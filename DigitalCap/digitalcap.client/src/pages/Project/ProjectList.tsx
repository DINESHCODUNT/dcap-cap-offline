import React, { useState, useEffect, useCallback } from 'react';
import axios from 'axios';
import { DragDropContext, Droppable, Draggable } from '@hello-pangea/dnd';
import Modal from 'react-bootstrap/Modal';
import { AbsSearch, AbsSelect, Button, BUTTONS, AbsReactTable, Toggle } from 'customer_portal-ui-shared';
import * as Icon from "react-bootstrap-icons";
import * as projectService from '../../service/ProjectService';
import moment from 'moment';
import EmptyProjectView from './EmptyProjectView';
import SyncConfirmModal from './SyncConfirmModal';
import { isElectron, isAppOnline } from '../../utils/runtime';

export interface SidebarProjectRow {
    name: string;
    imo: string;
    date: string;
    pct: number;
    status: string;
    id?: number;
    wcn?: string;
    clientName?: string;
    capRegion?: string;
    pidNumber?: string;
    capType?: string;
    capScope?: string;
    projectStatusColor?: string;
    projectStatusName?: string;
    statusName?: string;
    createdDate?: string;
    createdBy?: string;
    lastModifiedDate?: string;
    lastModifiedBy?: string;
    isReadOnlyEnabled?: boolean;
    projectLastUpdatedDateFormatted?: string;
    priority?: number;
    isDownload?: string;
    mostRecentComment?: string;
    commentCreatedBy?: string;
    commentLastModifiedDate?: string;
    commentFormattedDate?: string;
}

export interface ProjectListProps {
    selectedProject: SidebarProjectRow | null;
    onSelectProject: (project: SidebarProjectRow | null) => void;
    refreshTrigger?: number;
    onSidebarStats?: (stats: projectService.ProjectSidebarStatsAggregate) => void;
    onStatusChartStats?: (stats: projectService.ProjectSidebarStatsAggregate | null) => void;
    onRegionChartStats?: (stats: projectService.ProjectSidebarStatsAggregate | null) => void;
    onProjectsLoaded?: (projects: SidebarProjectRow[]) => void;
    externalSearchTerm?: string;
    externalStatusFilter?: string | null;
    externalRegionFilter?: string | null;
    navigatedProjectId?: number;
    onClearExternalFilters?: () => void;
}

const LOAD_COUNT = 20;

const getProjectStatusTag = (statusName?: string): string => {
    if (!statusName) return '';
    return statusName.trim().toLowerCase().replace(/\s+/g, '-');
};

const isGlobalSearchMatch = (p: SidebarProjectRow, trimmed: string) => {
    return (
        p.name?.toLowerCase().includes(trimmed) ||
        p.imo?.toLowerCase().includes(trimmed) ||
        p.pidNumber?.toLowerCase().includes(trimmed) ||
        p.capRegion?.toLowerCase().includes(trimmed) ||
        p.clientName?.toLowerCase().includes(trimmed) ||
        p.wcn?.toLowerCase().includes(trimmed) || false
    );
};

const ProjectList: React.FC<ProjectListProps> = ({
    onSelectProject,
    refreshTrigger = 0,
    onSidebarStats,
    onStatusChartStats,
    onRegionChartStats,
    onProjectsLoaded,
    externalSearchTerm = '',
    externalStatusFilter = null,
    externalRegionFilter = null,
    navigatedProjectId,
}) => {
    const [val, updateVal] = useState<string>("");
    const [statusFilter, setStatusFilter] = useState<string | null>(null);
    const [regionFilter, setRegionFilter] = useState<string | null>(null);
    const [connectionFilter, setConnectionFilter] = useState<'online' | 'offline' | null>(null);

    const [projects, setProjects] = useState<SidebarProjectRow[]>([]);
    const [allProjects, setAllProjects] = useState<SidebarProjectRow[]>([]);
    const [modalProject, setModalProject] = React.useState<SidebarProjectRow | null>(null);
    const [targetProjectToUpdate, setTargetProjectToUpdate] = React.useState<SidebarProjectRow | null>(null);
    const [selectedPriority, setSelectedPriority] = React.useState<number | null>(null);
    const [modalItems, setModalItems] = useState<{ id: string, originalPriority: number, priority: number, project: SidebarProjectRow | null, isCurrent: boolean }[]>([]);
    const [loading, setLoading] = useState(false);
    const [loadSuccess, setLoadSuccess] = useState(false);
    const [loadError, setLoadError] = useState<string | null>(null);
    const [downloadingProjectName, setDownloadingProjectName] = useState<string | null>(null);
    const [isDownloadInBackground, setIsDownloadInBackground] = useState<boolean>(false);
    const [downloadAbortController, setDownloadAbortController] = useState<AbortController | null>(null);
    const [transferError, setTransferError] = useState<string | null>(null);
    const isDesktop = isElectron();
    const [availableOnlineProjects, setAvailableOnlineProjects] = useState<projectService.DropdownProject[]>([]);
    const [loadingOnlineProjects, setLoadingOnlineProjects] = useState<boolean>(false);
    const [selectedOnlineProjectId, setSelectedOnlineProjectId] = useState<number | null>(null);
    const [syncConfirmProject, setSyncConfirmProject] = useState<SidebarProjectRow | null>(null);
    const [isDownloading, setIsDownloading] = useState<boolean>(false);
    const [downloadSuccessModal, setDownloadSuccessModal] = useState<boolean>(false);
    const [showDownloadConfirm, setShowDownloadConfirm] = useState<boolean>(false);
    const [showDownloadWarning, setShowDownloadWarning] = useState<boolean>(false);
    const [pageAppliedFilters, setPageAppliedFilters] = useState<{
        pageIndex: number;
        pageSize: string;
        resetPageIndex: boolean;
        filters: Record<string, unknown>;
        searchType?: string;
    }>({
        pageIndex: 0,
        pageSize: "50",
        resetPageIndex: false,
        filters: {},
        searchType: "list"
    });

    const [, setVisibleCount] = useState(LOAD_COUNT);


    useEffect(() => {
        if (modalProject) {
            setTargetProjectToUpdate(modalProject);
            const items = [1, 2, 3, 4, 5, 6, 7, 8, 9].map(p => ({
                id: `priority-${p}`,
                originalPriority: p,
                priority: p,
                project: allProjects.find(x => x.priority === p) || null,
                isCurrent: modalProject.priority === p
            }));

            if (!modalProject.priority) {
                items.push({
                    id: `priority-unassigned`,
                    originalPriority: 10,
                    priority: 10,
                    project: modalProject,
                    isCurrent: true
                });
            }

            setModalItems(items);
            setSelectedPriority(modalProject.priority || null);
        }
    }, [modalProject, allProjects]);

    const handleDragEnd = (result: any) => {
        if (!result.destination) return;

        const sourceIndex = result.source.index;
        const destinationIndex = result.destination.index;

        if (sourceIndex === destinationIndex) return;

        const draggedItem = modalItems[sourceIndex];
        const targetPriority = destinationIndex + 1;

        if (targetPriority > 9) return;
        if (!draggedItem || !draggedItem.project) return;

        setTargetProjectToUpdate(draggedItem.project);
        const updatedProjects = modalItems
            .filter(item => item.project)
            .map(item => {
                if (item.project?.id === draggedItem.project?.id) {
                    return { ...item, computedPriority: targetPriority, isCurrent: true };
                } else {
                    let computed = item.originalPriority;
                    if (computed >= targetPriority) {
                        computed = computed + 1;
                    }
                    return { ...item, computedPriority: computed, isCurrent: false };
                }
            });

        const newItems = [1, 2, 3, 4, 5, 6, 7, 8, 9].map(p => {
            const found = updatedProjects.find(x => x.computedPriority === p);
            return {
                id: `priority-${p}`,
                originalPriority: found ? found.originalPriority : p,
                priority: p,
                project: found ? found.project : null,
                isCurrent: found ? found.isCurrent : false
            };
        });

        setModalItems(newItems);
        setSelectedPriority(targetPriority);
    };


    const loadProjects = useCallback(async () => {

        setLoading(true);
        setLoadError(null);

        try {

            const response = await projectService.getAllProjectsGridData();

            const raw = response.data;

            const items = projectService.normalizeAllProjectsGridPayload(raw);

            const mapped: SidebarProjectRow[] = items.map((project: any) => {

                let formattedDate = '';

                if (
                    project.commentLastModifiedDate &&
                    !project.commentLastModifiedDate.includes('1901') &&
                    !project.commentLastModifiedDate.includes('1900')
                ) {
                    const parsed = moment(project.commentLastModifiedDate);

                    formattedDate = parsed.isValid()
                        ? parsed.format('DD-MMM-YYYY')
                        : project.commentLastModifiedDate;
                }

                let formattedLastModifiedDate = '';
                if (project.projectLastUpdatedDate) {
                    const parsedLMD = moment(project.projectLastUpdatedDate);
                    formattedLastModifiedDate = parsedLMD.isValid() ? parsedLMD.format('DD-MMM-YYYY hh:mm A') : project.projectLastUpdatedDate;
                }

                let formattedProjectLastUpdatedDate = '';
                if (project.projectLastUpdatedDate) {
                    const parsedLUD = moment(project.projectLastUpdatedDate);
                    formattedProjectLastUpdatedDate = parsedLUD.isValid() ? parsedLUD.format('DD-MMM-YYYY') : project.projectLastUpdatedDate;
                }

                let formattedCommentDate = '';
                if (project.commentLastModifiedDate) {
                    const parsedCMD = moment(project.commentLastModifiedDate);
                    formattedCommentDate = parsedCMD.isValid() ? parsedCMD.format('DD-MMM h:mmA') : '';
                }

                return {
                    name: (project.name || '').trim().replace(/\t/g, ''),
                    imo: project.imo || '',
                    date: formattedDate,
                    pct: project.percentComplete || 0,
                    status: projectService.getSidebarConnectionStatus(project),
                    id: project.id,
                    wcn: project.wcn || '',
                    clientName: project.companyName || '',
                    capRegion: project.capRegion || '',
                    pidNumber: project.pidNumber || '',
                    projectStatusName: project.projectStatusName || '',
                    projectStatusColor: project.projectStatusColor || '',
                    lastModifiedDate: formattedLastModifiedDate,
                    lastModifiedBy: project.lastModifiedBy || project.modifiedBy || project.commentCreatedBy || '',
                    isReadOnlyEnabled: Boolean(project.isReadOnlyEnabled),
                    projectLastUpdatedDateFormatted: formattedProjectLastUpdatedDate,
                    priority: project.projectPriority ? parseInt(project.projectPriority, 10) : undefined,
                    isDownload: project.isDownload || 'Online',
                    mostRecentComment: project.mostRecentComment || '',
                    commentCreatedBy: project.commentCreatedBy || '',
                    commentLastModifiedDate: project.commentLastModifiedDate || '',
                    commentFormattedDate: formattedCommentDate
                };
            });


            mapped.sort((a, b) => {
                if (a.priority !== undefined && b.priority !== undefined) {
                    return a.priority - b.priority;
                }
                if (a.priority !== undefined) return -1;
                if (b.priority !== undefined) return 1;
                return 0;
            });


            setProjects(mapped);

            setAllProjects(mapped);

            setLoadSuccess(true);
            onProjectsLoaded?.(mapped);

        } catch (error: any) {
            setLoadSuccess(false);
            setLoadError(error?.message || "Failed to load projects");
        } finally {

            setLoading(false);
        }
    }, [onProjectsLoaded, isDesktop]);

    const loadAvailableOnlineProjects = useCallback(async () => {
        if (!isDesktop) return;
        setLoadingOnlineProjects(true);
        try {
            const res = await projectService.getAvailableOnlineProjects();
            if (Array.isArray(res.data)) {
                setAvailableOnlineProjects(res.data);
            }
        } catch {
        } finally {
            setLoadingOnlineProjects(false);
        }
    }, [isDesktop]);

    const handleTriggerDownloadFromDropdown = async () => {
        if (!selectedOnlineProjectId) return;
        const match = availableOnlineProjects.find(p => p.projectId === selectedOnlineProjectId);
        if (!match) return;

        try {
            const checkRes = await projectService.getDownloadedDataExists(selectedOnlineProjectId);
            if (checkRes.data == null || !checkRes.data) {
                setShowDownloadConfirm(true);
            } else {
                setShowDownloadWarning(true);
            }
        } catch {
            setShowDownloadConfirm(true);
        }
    };

    const handleProceedDropdownDownload = async () => {
        setShowDownloadConfirm(false);
        if (!selectedOnlineProjectId) return;
        const match = availableOnlineProjects.find(p => p.projectId === selectedOnlineProjectId);
        if (!match) return;
        setIsDownloading(true);
        setIsDownloadInBackground(false);
        const controller = new AbortController();
        setDownloadAbortController(controller);
        setDownloadingProjectName(match.projectName);
        try {
            const response = await projectService.downloadProjectOffline(selectedOnlineProjectId, controller.signal);
            if (response.data && response.data.success === false) {
                setTransferError(response.data.message || "Unable to download project.");
            } else {
                setDownloadSuccessModal(true);
                await loadProjects();
                await loadAvailableOnlineProjects();
            }
        } catch (err: any) {
            if (axios.isCancel(err) || err?.name === 'CanceledError') {
                return;
            }
            setTransferError("Unable to Sync/Download due to a very low internet connection. Please proceed when your internet connection is having at least 10 mbps download and upload speed.");
        } finally {
            setIsDownloading(false);
            setDownloadAbortController(null);
            setDownloadingProjectName(null);
        }
    };

    const handleStopDropdownDownload = () => {
        if (downloadAbortController) {
            downloadAbortController.abort();
        }
        setIsDownloading(false);
        setIsDownloadInBackground(false);
        setDownloadingProjectName(null);
    };

    useEffect(() => {
        if (isDesktop) {
            loadAvailableOnlineProjects();
        }
    }, [isDesktop, loadAvailableOnlineProjects]);

    useEffect(() => {
        if (!navigatedProjectId || allProjects.length === 0) return;

        const idx = allProjects.findIndex(p => p.id === navigatedProjectId);
        if (idx === -1) return;

        setVisibleCount(Math.max(idx + 1, LOAD_COUNT));

        setTimeout(() => {
            const el = document.getElementById(`project-item-${navigatedProjectId}`);
            el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }, 300);

    }, [navigatedProjectId, allProjects]);

    useEffect(() => {
        if (externalSearchTerm === undefined) return;

        updateVal(externalSearchTerm);
        const trimmed = externalSearchTerm.trim().toLowerCase();

        if (trimmed === '') {
            setProjects(allProjects);
        } else {
            const filtered = allProjects.filter(p =>
                isGlobalSearchMatch(p, trimmed)
            );
            setProjects(filtered);
        }
        setVisibleCount(LOAD_COUNT);

    }, [externalSearchTerm, allProjects]);

    useEffect(() => {
        if (externalStatusFilter !== undefined) {
            setStatusFilter(externalStatusFilter);
        }
    }, [externalStatusFilter]);

    useEffect(() => {
        if (externalRegionFilter !== undefined) {
            setRegionFilter(externalRegionFilter);
        }
    }, [externalRegionFilter]);

    useEffect(() => {

        void loadProjects();

    }, [refreshTrigger]);

    useEffect(() => {
        const handleRefresh = () => {
            void loadProjects();
        };
        window.addEventListener('refreshProjectList', handleRefresh);
        return () => {
            window.removeEventListener('refreshProjectList', handleRefresh);
        };
    }, [loadProjects]);

    useEffect(() => {

        const fetchStats = async () => {

            try {

                const stats = await projectService.getProjectStats();

                onSidebarStats?.(stats);

            } catch (err) {

            }
        };

        void fetchStats();

    }, [refreshTrigger]);

    const onChangeSearchInput = useCallback((value: string) => {

        updateVal(value);

        const trimmed = value.trim().toLowerCase();

        if (!trimmed) {

            setProjects(allProjects);

        } else {

            const filtered = allProjects.filter(p =>
                isGlobalSearchMatch(p, trimmed)
            );

            setProjects(filtered);
        }

        setVisibleCount(LOAD_COUNT);

    }, [allProjects]);

    const handleClear = useCallback(() => {
        updateVal("");
        setProjects(allProjects);
        setVisibleCount(LOAD_COUNT);
        setStatusFilter(null);
        setRegionFilter(null);
        setConnectionFilter(null);
        setPageAppliedFilters(prev => ({
            ...prev,
            pageIndex: 0,
            resetPageIndex: true,
            filters: {}
        }));
    }, [allProjects]);

    const cleanString = (str?: string | null) =>
        str
            ? str
                .toString()
                .normalize("NFKD")
                .replace(/\s+/g, " ")
                .replace(/[\u200B-\u200D\uFEFF]/g, "")
                .trim()
                .toLowerCase()
            : "";

    const applyColumnFilters = (
        data: SidebarProjectRow[],
        activeFilters: Record<string, unknown>,
        skipKey?: string | null
    ) => {
        const filterEntries = Object.entries(activeFilters);
        if (filterEntries.length === 0) {
            return data;
        }

        return data.filter((row) =>
            filterEntries.every(([columnId, filter]) => {
                if (columnId === skipKey) return true;

                const { searchTerm, selectedFilters } = filter as {
                    searchTerm?: string;
                    selectedFilters?: unknown[];
                };

                if (selectedFilters && selectedFilters.length > 0) {
                    const rowValue = (row as any)[columnId];
                    if (typeof rowValue === 'boolean') {
                        return selectedFilters.some(f => {
                            const fStr = String(f).toLowerCase();
                            if (fStr === 'active' || fStr === 'true' || fStr === 'yes' || fStr === 'checked') {
                                return rowValue === true;
                            }
                            if (fStr === 'inactive' || fStr === 'false' || fStr === 'no' || fStr === 'unchecked') {
                                return rowValue === false;
                            }
                            return false;
                        });
                    }
                    return selectedFilters.some(f => {
                        const fStr = cleanString(String(f));
                        const rStr = cleanString(String(rowValue));
                        if (fStr === "n/a") {
                            return rStr === "" || rStr === "n/a" || rStr === "-";
                        }
                        return fStr === rStr;
                    });
                }

                if (!searchTerm) return true;

                const rowValue = cleanString((row as any)[columnId] || '');
                const searchStr = cleanString(searchTerm);
                if (searchStr === "n/a" && (rowValue === "" || rowValue === "-")) return true;
                return rowValue.includes(searchStr);
            })
        );
    };

    const columnUniqueValues = React.useMemo(() => {
        const filters = pageAppliedFilters.filters || {};

        const computeUniq = (targetKey: keyof SidebarProjectRow) => {
            let src = allProjects || [];

            if (val.trim() !== "") {
                const trimmed = val.trim().toLowerCase();
                src = src.filter(p =>
                    isGlobalSearchMatch(p, trimmed)
                );
            }
            if (statusFilter) {
                src = src.filter(p => p.status?.toLowerCase() === statusFilter.toLowerCase() || p.projectStatusName?.toLowerCase() === statusFilter.toLowerCase() || getProjectStatusTag(p.projectStatusName) === statusFilter.toLowerCase());
            }
            if (regionFilter) {
                const cleanFilterRegion = regionFilter.toLowerCase().replace(/\s+/g, '');
                src = src.filter(p => (p.capRegion?.toLowerCase().replace(/\s+/g, '') || '') === cleanFilterRegion);
            }

            const filteredSrc = applyColumnFilters(src, filters, targetKey);
            const vals = Array.from(new Set(filteredSrc.map(r => {
                const v = r[targetKey];
                return (v == null || String(v).trim() === '') ? "N/A" : String(v);
            })));
            vals.sort();
            const naIndex = vals.indexOf("N/A");
            if (naIndex > 0) {
                vals.splice(naIndex, 1);
                vals.unshift("N/A");
            }
            return vals;
        };

        return {
            name: computeUniq('name'),
            imo: computeUniq('imo'),
            pidNumber: computeUniq('pidNumber'),
            capRegion: computeUniq('capRegion'),
            date: computeUniq('date'),
            clientName: computeUniq('clientName'),
            wcn: computeUniq('wcn'),
            pct: computeUniq('pct')
        };
    }, [allProjects, pageAppliedFilters.filters, val, statusFilter, regionFilter, connectionFilter]);

    const columns = React.useMemo<Array<any>>(
        () => {
            const baseColumns: any[] = [
                {
                    label: "Project Name",
                    accessorKey: "name",
                    accessor: "name",
                    columnValues: { type: "both", data: columnUniqueValues.name },
                    Cell: ({ row }: any) => {
                        const { original } = row;
                        return (
                            <div className="d-flex align-items-center justify-content-start gap-3 rank-list flex-row">
                                <div className="d-flex gap-2">
                                    <div className={`project-dot priority ${!original.priority ? 'empty' : ''}`} onClick={(e) => { e.stopPropagation(); setModalProject(original); setSelectedPriority(null); }}>
                                        {original.priority || ''}
                                    </div>
                                </div>
                                <div className="d-flex gap-2 flex-column">
                                    <p className="mr-2 _600 mb-0 text-primary text-link cursor-pointer" role="button" onClick={() => onSelectProject(original)}>
                                        {original.name}
                                    </p>
                                    {!isDesktop && (
                                        <p className="text-muted small mb-0">
                                            IMO <span className="_600 ">{original.imo}</span>
                                        </p>
                                    )}
                                    {isDesktop && (
                                        <>
                                            <div className="d-flex align-items-center gap-3 text-muted small mt-1">
                                                <span className="d-inline-flex align-items-center gap-1">
                                                    <Icon.CheckCircle size={14} />
                                                    <b>{original.pct ?? original.percentComplete ?? 0}% Completed</b>
                                                </span>
                                                {original.projectLastUpdatedDateFormatted && (
                                                    <span className="d-inline-flex align-items-center gap-1">
                                                        <Icon.Clock size={14} />
                                                        <b>Last Updated {original.projectLastUpdatedDateFormatted}</b>
                                                    </span>
                                                )}
                                            </div>
                                            {original.commentFormattedDate && (
                                                <div className="d-flex align-items-center gap-2 text-muted small mt-1">
                                                    <Icon.ChatRightTextFill size={14} className="flex-shrink-0" />
                                                    <span>
                                                        <b>{original.commentCreatedBy} ({original.commentFormattedDate}) </b>
                                                        {original.mostRecentComment}
                                                    </span>
                                                </div>
                                            )}
                                        </>
                                    )}
                                </div>
                            </div>
                        );
                    },
                },
            ];

            if (isDesktop) {
                baseColumns.push({
                    label: "IMO",
                    accessorKey: "imo",
                    accessor: "imo",
                    columnValues: { type: "both", data: columnUniqueValues.imo },
                    Cell: ({ value }: any) => (value && value.toString().trim() !== '') ? value : '-',
                });
            }

            baseColumns.push(
                {
                    label: "Project ID",
                    accessorKey: "pidNumber",
                    accessor: "pidNumber",
                    columnValues: { type: "both", data: columnUniqueValues.pidNumber },
                    Cell: ({ value }: any) => (value !== undefined && value !== null && value.toString().trim() !== '') ? value : (isDesktop ? 0 : '-'),
                },
                {
                    label: "CAP Coordinator",
                    accessorKey: "capRegion",
                    accessor: "capRegion",
                    columnValues: { type: "both", data: columnUniqueValues.capRegion },
                    Cell: ({ value }: any) => (value && value.toString().trim() !== '') ? value : '-',
                },
                {
                    label: "Potential Drydock Date",
                    accessorKey: "date",
                    accessor: "date",
                    columnValues: { type: "both", data: columnUniqueValues.date },
                    Cell: ({ value }: any) => (value && value.toString().trim() !== '') ? value : '-',
                },
                {
                    label: isDesktop ? "Client" : "Client Name",
                    accessorKey: "clientName",
                    accessor: "clientName",
                    columnValues: { type: "both", data: columnUniqueValues.clientName },
                    Cell: ({ value }: any) => (value && value.toString().trim() !== '') ? value : '-',
                },
                {
                    label: isDesktop ? "WCN" : "WCN Number",
                    accessorKey: "wcn",
                    accessor: "wcn",
                    columnValues: { type: "both", data: columnUniqueValues.wcn },
                    Cell: ({ value }: any) => (value && value.toString().trim() !== '') ? value : '-',
                }
            );

            if (!isDesktop) {
                baseColumns.push({
                    label: "Completion %",
                    accessorKey: "pct",
                    accessor: "pct",
                    columnValues: { type: "both", data: columnUniqueValues.pct },
                    Cell: ({ value }: any) => `${value ?? 0}%`,
                });
            }

            baseColumns.push({
                label: "Status",
                accessorKey: "status",
                accessor: "status",
                filtering: false,
                sorting: false,
                Cell: ({ row: { original } }: any) => (
                    <div className="d-flex flex-column gap-1">
                        <span className={`project-conn tag-${original.status}`}>
                            {original.isDownload ? original.isDownload : (original.status.charAt(0).toUpperCase() + original.status.slice(1))}
                        </span>
                        {original.projectStatusName && (
                            <span className={`project-conn tag-${getProjectStatusTag(original.projectStatusName)}`} style={{ color: original.projectStatusColor || 'inherit' }}>
                                {original.projectStatusName}
                            </span>
                        )}
                    </div>
                ),
            });

            if (!isDesktop) {
                baseColumns.push({
                    label: "Modified by",
                    accessorKey: "lastModifiedBy",
                    accessor: "lastModifiedBy",
                    filtering: false,
                    sorting: false,
                    Cell: ({ row: { original } }: any) => (
                        <div className="d-flex flex-column gap-1">
                            <span>{original.lastModifiedBy || 'N/A'}</span>
                            <span className="text-muted small">{original.lastModifiedDate || ''}</span>
                        </div>
                    )
                });
            }

            baseColumns.push({
                label: "Action",
                accessorKey: "action",
                accessor: "action",
                filtering: false,
                sorting: false,
                Cell: ({ row: { original } }: any) => (
                    <div className='d-flex align-items-center justify-content-start gap-3'>
                        <Button
                            variant={BUTTONS.TABLEDOWNLOAD}
                            className="project-action-btn"
                            style={{ width: '32px', height: '32px', minWidth: '32px', padding: 0, borderRadius: '6px' }}
                            startIcon={<Icon.ArrowRight size={16} />}
                            onClick={() => onSelectProject(original)}
                        ></Button>
                        {isDesktop && navigator.onLine && (
                            <button
                                type="button"
                                className="btn btn-sm btn-outline-success p-1 d-flex align-items-center justify-content-center"
                                style={{ borderRadius: '6px', width: '32px', height: '32px' }}
                                onClick={(e) => {
                                    e.stopPropagation();
                                    setSyncConfirmProject(original);
                                }}
                                title="Save to Online"
                            >
                                <Icon.ArrowRepeat size={16} />
                            </button>
                        )}
                    </div>
                )
            });

            return baseColumns;
        },
        [projects, onSelectProject, columnUniqueValues, isDesktop]
    );

    useEffect(() => {
        if (!onStatusChartStats || !onRegionChartStats || projects.length === 0) return;

        const statusChartProjects = projects.filter(p => {
            if (externalRegionFilter) {
                return p.capRegion?.toLowerCase() === externalRegionFilter.toLowerCase();
            }
            return true;
        });

        const regionChartProjects = projects.filter(p => {
            if (externalStatusFilter) {
                return p.status?.toLowerCase() === externalStatusFilter.toLowerCase() ||
                    p.projectStatusName?.toLowerCase() === externalStatusFilter.toLowerCase() ||
                    getProjectStatusTag(p.projectStatusName) === externalStatusFilter.toLowerCase();
            }
            return true;
        });

        onStatusChartStats(projectService.aggregateProjectStatsFromGridRows(statusChartProjects as any));
        onRegionChartStats(projectService.aggregateProjectStatsFromGridRows(regionChartProjects as any));
    }, [projects, externalStatusFilter, externalRegionFilter, onStatusChartStats, onRegionChartStats]);

    const allFilteredProjects = projects.filter(p => {
        let matchStatus = true;
        if (statusFilter) {
            matchStatus = p.status?.toLowerCase() === statusFilter.toLowerCase() ||
                p.projectStatusName?.toLowerCase() === statusFilter.toLowerCase() ||
                getProjectStatusTag(p.projectStatusName) === statusFilter.toLowerCase();
        }

        let matchRegion = true;
        if (regionFilter) {
            matchRegion = p.capRegion?.toLowerCase() === regionFilter.toLowerCase();
        }

        let matchConnection = true;
        if (connectionFilter) {
            matchConnection = p.status?.toLowerCase() === connectionFilter;
        }

        return matchStatus && matchRegion && matchConnection;
    });

    const fullyFilteredProjects = applyColumnFilters(allFilteredProjects, pageAppliedFilters.filters);

    const getPageSizeNum = (pageSize?: string | number) => {
        if (pageSize != null && pageSize !== "") {
            return typeof pageSize === 'string' ? parseInt(pageSize, 10) : Number(pageSize);
        }
        return 50;
    };

    const applyFilter = React.useCallback((filtersObj: any) => {
        const filteredData = Object.fromEntries(
            Object.entries(filtersObj.filters || {}).filter(
                ([, value]) => (value as { searchTerm: string })?.searchTerm !== ""
                    || ((value as { selectedFilters?: string[] })?.selectedFilters?.length ?? 0) > 0
                    || (value as { isFiltersApplied?: boolean })?.isFiltersApplied
            )
        );

        setPageAppliedFilters(prev => {
            const prevFiltersStr = JSON.stringify(prev.filters || {});
            const newFiltersStr = JSON.stringify(filteredData);
            const filtersChanged = prevFiltersStr !== newFiltersStr;

            let pageIndexNum = typeof filtersObj.pageIndex === 'string'
                ? parseInt(filtersObj.pageIndex, 10)
                : Number(filtersObj?.pageIndex ?? 0);

            if (filtersChanged) {
                pageIndexNum = 0;
            }

            return {
                pageIndex: pageIndexNum,
                pageSize: String(filtersObj?.pageSize ?? "50"),
                resetPageIndex: filtersChanged,
                filters: filteredData,
                searchType: "list"
            };
        });
    }, []);

    React.useEffect(() => {
        setPageAppliedFilters(prev => ({ ...prev, pageIndex: 0, resetPageIndex: true }));
    }, [statusFilter, regionFilter, connectionFilter, projects]);

    const dataCount = fullyFilteredProjects.length;
    const pageSizeNum = getPageSizeNum(pageAppliedFilters.pageSize);
    const startIdx = pageAppliedFilters.pageIndex * pageSizeNum;
    const pagedProjects = fullyFilteredProjects.slice(startIdx, startIdx + pageSizeNum);

    useEffect(() => {
        let isMounted = true;
        if (!loading && loadSuccess && !loadError && isDesktop && allProjects.length === 0) {
            isAppOnline().then(async (online) => {
                if (!isMounted || !online) return;
                try {
                    const res: any = await projectService.getOnlineUrl();
                    if (isMounted && res?.data?.url) {
                        window.location.href = res.data.url;
                    }
                } catch {
                    // If fetching online URL fails, do not redirect
                }
            });
        }
        return () => {
            isMounted = false;
        };
    }, [loading, loadSuccess, loadError, isDesktop, allProjects.length]);

    return (
        <>
            <div className="container-fluid p-0 bg-transparent">
                <div className="section-title mt-4">
                    <h5 className='_700'>Projects List</h5>
                    <div className="section-actions"></div>
                </div>

                <div className="row align-items-center mb-3">
                    <div className="col-12 col-md-4 mt-2">
                        <div className="d-flex justify-content-start align-items-center gap-2">
                            <AbsSearch
                                handleClear={handleClear}
                                value={val}
                                handleChange={(e: any) => onChangeSearchInput(e.target.value)}
                                placeholder="Search"
                                searchApplied={val.trim() !== ""}
                            />
                            <a href="#" className='text-link text-nowrap _500' onClick={(e) => { e.preventDefault(); handleClear(); }}>Clear Filter</a>
                        </div>
                    </div>

                    <div className="col-12 col-md-8 d-flex justify-content-md-end gap-2 mt-3 mt-md-2">
                        <div className="d-flex justify-content-start justify-content-md-end align-items-center gap-2 overflow-x-auto flex-nowrap w-100 pb-1 hide-scrollbar">
                            <div
                                className="d-inline-flex align-items-center me-3 connection-toggle-container flex-shrink-0"
                                onClick={() => {
                                    setConnectionFilter(connectionFilter === 'online' ? 'offline' : 'online');
                                }}
                            >
                                <span className={`mb-0 _600 connection-toggle-label offline-label ${connectionFilter === 'offline' ? 'active' : ''}`}>Offline</span>
                                <Toggle
                                    id="connectionToggle"
                                    checked={connectionFilter === 'online'}
                                    onChange={() => { }}
                                />
                                <span className={`mb-0 _600 connection-toggle-label online-label ${connectionFilter === 'online' ? 'active' : ''}`}>Online</span>
                            </div>
                            {(() => {
                                const dynamicStatuses = (() => {
                                    const statusSet = new Set<string>();

                                    projects.forEach(p => {
                                        if (p.projectStatusName) {
                                            statusSet.add(p.projectStatusName.trim());
                                        }
                                    });
                                    const arr = Array.from(statusSet).sort().map(s => ({
                                        label: s,
                                        value: s.toLowerCase()
                                    }));

                                    return [{ label: 'All', value: 'all' }, ...arr];
                                })();

                                const getCount = (val: string) => {
                                    if (val === 'all') return projects.length;
                                    return projects.filter(p => p.status?.toLowerCase() === val || p.projectStatusName?.toLowerCase() === val || getProjectStatusTag(p.projectStatusName) === val).length;
                                };

                                return dynamicStatuses.map((option) => {
                                    const isAll = option.value === 'all';
                                    const isSelected = isAll ? statusFilter === null : statusFilter === option.value;

                                    return (
                                        <span
                                            key={option.value}
                                            role="button"
                                            className={`project-status-badge flex-shrink-0 ${option.value} ${isSelected ? 'is-selected' : ''}`}
                                            onClick={() => setStatusFilter(isAll || isSelected ? null : option.value)}
                                        >
                                            {option.label} - {getCount(option.value)}
                                        </span>
                                    );
                                });
                            })()}
                        </div>
                    </div>
                </div>

                {isDesktop && navigator.onLine && (
                    <div className="d-flex justify-content-between align-items-center mb-3 p-3 bg-white rounded shadow-sm">
                        <h4 className="mb-0 _600">All Projects</h4>
                        <div className="d-flex align-items-center gap-2">
                            <div style={{ minWidth: '260px' }}>
                                <AbsSelect
                                    mult={false}
                                    search={true}
                                    placeholder="Select project"
                                    options={availableOnlineProjects.map((p) => ({
                                        text: p.projectName,
                                        value: String(p.projectId)
                                    }))}
                                    selected={selectedOnlineProjectId ? [String(selectedOnlineProjectId)] : []}
                                    onChange={(selected) => setSelectedOnlineProjectId(selected?.[0] ? Number(selected[0]) : null)}
                                    disabled={loadingOnlineProjects || isDownloading}
                                />
                            </div>
                            <Button
                                variant={BUTTONS.PRIMARY}
                                className="text-nowrap"
                                onClick={handleTriggerDownloadFromDropdown}
                                disabled={!selectedOnlineProjectId || isDownloading}
                            >
                                Download Offline
                            </Button>
                        </div>
                    </div>
                )}

                {loadError ? (
                    <div className="alert alert-danger mt-3" role="alert">
                        {loadError}
                    </div>
                ) : !loading && allProjects.length === 0 ? (
                    <EmptyProjectView />
                ) : (
                    <>
                        {isDesktop && (
                            <div className="alert alert-danger d-flex align-items-center gap-2 mb-3 py-2 px-3" style={{ fontSize: '14px', borderRadius: '8px' }}>
                                <div>
                                    <span>Ensure your network connection is having at least 10mbps download and upload speeds when either downloading or saving the project to online.</span>
                                </div>
                            </div>
                        )}
                        <div className="row">
                            <div className="col-12 overflow-x-auto custom-table-responsive">
                                <AbsReactTable
                                    columns={columns}
                                    data={pagedProjects}
                                    pagination={true}
                                    expand={false}
                                    allowColumnsShowHide={true}
                                    shadow={true}
                                    allDataProvided
                                    dataCount={dataCount}
                                    isloading={loading}
                                    pageOptions={["50", "100", "150", "200"]}
                                    applyFilterCallback={applyFilter}
                                    appliedFilters={{
                                        ...pageAppliedFilters,
                                        pageIndex: pageAppliedFilters.pageIndex.toString(),
                                        pageSize: pageAppliedFilters.pageSize,
                                        resetPageIndex: pageAppliedFilters.resetPageIndex,
                                        searchType: "all"
                                    }}
                                />
                            </div>
                        </div>
                    </>
                )}
            </div>

            {modalProject && (
                <Modal show={!!modalProject} onHide={() => setModalProject(null)}>
                    <Modal.Header closeButton>
                        <Modal.Title>Assign Priority</Modal.Title>
                    </Modal.Header>
                    <Modal.Body>
                        <p>Select a priority position below. Lower priority positions will be updated as a result.</p>

                        <div className="priority-list-view">
                            <DragDropContext onDragEnd={handleDragEnd}>
                                <Droppable droppableId="priority-list">
                                    {(provided) => (
                                        <div
                                            {...provided.droppableProps}
                                            ref={provided.innerRef}
                                        >
                                            {modalItems.map((item, index) => {
                                                const existingProject = item.project;
                                                const isCurrent = item.isCurrent;
                                                const isSelected = selectedPriority === item.priority;

                                                return (
                                                    <Draggable
                                                        key={item.id}
                                                        draggableId={item.id}
                                                        index={index}
                                                        isDragDisabled={!existingProject}
                                                    >
                                                        {(provided, snapshot) => (
                                                            <div
                                                                ref={provided.innerRef}
                                                                {...provided.draggableProps}
                                                                {...provided.dragHandleProps}
                                                                className={`priority-list-item ${isCurrent ? 'current' : ''} ${isSelected ? 'selected' : ''}`}
                                                                onClick={() => setSelectedPriority(item.priority)}
                                                                style={{
                                                                    display: 'flex',
                                                                    alignItems: 'center',
                                                                    padding: '10px 15px',
                                                                    marginBottom: '8px',
                                                                    border: `1px solid ${isCurrent || isSelected ? '#007bff' : '#ddd'}`,
                                                                    borderRadius: '5px',
                                                                    backgroundColor: snapshot.isDragging ? '#cce5ff' : (isCurrent ? '#e6f2ff' : isSelected ? '#f8f9fa' : '#fff'),
                                                                    cursor: existingProject ? (snapshot.isDragging ? 'grabbing' : 'pointer') : 'pointer',
                                                                    transition: 'background-color 0.2s',
                                                                    boxShadow: snapshot.isDragging ? '0 5px 15px rgba(0,0,0,0.1)' : 'none',
                                                                    ...provided.draggableProps.style
                                                                }}
                                                            >
                                                                <div style={{ marginRight: '15px', color: '#888', cursor: existingProject ? 'inherit' : 'default' }}>
                                                                    <svg width="12" height="16" viewBox="0 0 12 16" fill="currentColor">
                                                                        <path d="M4 14c0 1.1-.9 2-2 2s-2-.9-2-2 .9-2 2-2 2 .9 2 2zm8 0c0 1.1-.9 2-2 2s-2-.9-2-2 .9-2 2-2 2 .9 2 2zM4 8c0 1.1-.9 2-2 2s-2-.9-2-2 .9-2 2-2 2 .9 2 2zm8 0c0 1.1-.9 2-2 2s-2-.9-2-2 .9-2 2-2 2 .9 2 2zM4 2c0 1.1-.9 2-2 2S0 3.1 0 2 0 0 2 0s2 .9 2 2zm8 0c0 1.1-.9 2-2 2s-2-.9-2-2 .9-2 2-2 2 .9 2 2z" />
                                                                    </svg>
                                                                </div>
                                                                <div
                                                                    className="project-dot priority"
                                                                    style={{
                                                                        marginRight: '15px',
                                                                        position: 'static',
                                                                        width: '24px',
                                                                        height: '24px',
                                                                        backgroundColor: isCurrent || isSelected ? '#007bff' : '#eee',
                                                                        color: isCurrent || isSelected ? '#fff' : '#333'
                                                                    }}
                                                                >
                                                                    {item.priority === 10 ? '-' : item.priority}
                                                                </div>
                                                                <div style={{ flex: 1, fontWeight: isCurrent ? 'bold' : 'normal' }}>
                                                                    {existingProject ? existingProject.name : 'Empty Position'}
                                                                </div>
                                                                {isCurrent && (
                                                                    <div style={{
                                                                        fontSize: '11px',
                                                                        color: '#007bff',
                                                                        border: '1px solid #007bff',
                                                                        borderRadius: '4px',
                                                                        padding: '2px 6px'
                                                                    }}>
                                                                        current position
                                                                    </div>
                                                                )}
                                                            </div>
                                                        )}
                                                    </Draggable>
                                                );
                                            })}
                                            {provided.placeholder}
                                        </div>
                                    )}
                                </Droppable>
                            </DragDropContext>
                        </div>

                    </Modal.Body>
                    <Modal.Footer>
                        <div className="d-flex gap-2">
                            <Button
                                variant={BUTTONS.PRIMARY}
                                onClick={async () => {
                                    if (targetProjectToUpdate?.id) {
                                        try {
                                            await projectService.updatePriority(targetProjectToUpdate.id, selectedPriority);
                                            setModalProject(null);
                                            loadProjects();
                                        } catch (error) {
                                        }
                                    }
                                }}
                            >
                                Confirm
                            </Button>
                            <Button variant={BUTTONS.SECONDARY} onClick={() => setModalProject(null)}>Cancel</Button>
                        </div>
                    </Modal.Footer>

                </Modal>
            )}

            {/* Download Confirm Modal */}
            <Modal
                show={showDownloadConfirm}
                onHide={() => setShowDownloadConfirm(false)}
                centered
            >
                <Modal.Header closeButton>
                    <Modal.Title>Confirm</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    <div className="d-flex align-items-center gap-3">
                        <div className="img-wrapper">
                            <i className="fas fa-exclamation text-warning" style={{ fontSize: '24px' }}></i>
                        </div>
                        <div className="alert-content">
                            <h5>Are you sure?</h5>
                            <p className="mb-0">
                                Download offline mode which include all parts.<br />
                                Do you want to continue this?
                            </p>
                        </div>
                    </div>
                </Modal.Body>
                <Modal.Footer className="d-flex justify-content-end gap-2">
                    <Button variant={BUTTONS.SECONDARY} onClick={() => setShowDownloadConfirm(false)}>
                        Cancel
                    </Button>
                    <Button variant={BUTTONS.SECONDARY} onClick={handleProceedDropdownDownload}>
                        Yes, Proceed
                    </Button>
                </Modal.Footer>
            </Modal>

            {/* Download Warning Modal */}
            <Modal
                show={showDownloadWarning}
                onHide={() => setShowDownloadWarning(false)}
                centered
            >
                <Modal.Header closeButton>
                    <Modal.Title>Confirm</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    <div className="d-flex align-items-center gap-3">
                        <div className="img-wrapper">
                            <i className="fas fa-exclamation text-warning" style={{ fontSize: '24px' }}></i>
                        </div>
                        <div className="alert-content">
                            <h5>Are you sure?</h5>
                            <p className="mb-0">
                                This CAP Project already Downloaded,Please Check with Admin
                            </p>
                        </div>
                    </div>
                </Modal.Body>
                <Modal.Footer className="d-flex justify-content-end gap-2">
                    <Button variant={BUTTONS.SECONDARY} onClick={() => setShowDownloadWarning(false)}>
                        Ok
                    </Button>
                </Modal.Footer>
            </Modal>

            {/* Download In Progress Modal */}
            <Modal
                show={isDownloading && !isDownloadInBackground}
                onHide={handleStopDropdownDownload}
                centered
                backdrop="static"
            >
                <Modal.Header closeButton>
                </Modal.Header>
                <Modal.Body>
                    <div className="d-flex align-items-center gap-3 mb-3">
                        <div className="img-wrapper">
                            <i className="fas fa-download text-primary" style={{ fontSize: '24px' }}></i>
                        </div>
                        <div className="alert-content flex-grow-1">
                            <h5 className="mb-0">Downloading the {downloadingProjectName} project in Offline Mode</h5>
                        </div>
                    </div>
                    <div className="progress" style={{ height: '10px' }}>
                        <div
                            className="progress-bar progress-bar-striped progress-bar-animated bg-primary"
                            role="progressbar"
                            style={{ width: "100%" }}
                        />
                    </div>
                </Modal.Body>
                <Modal.Footer className="d-flex justify-content-end gap-2">
                    <Button variant={BUTTONS.SECONDARY} onClick={handleStopDropdownDownload}>
                        No, Cancel
                    </Button>
                    <Button variant={BUTTONS.SECONDARY} onClick={() => setIsDownloadInBackground(true)}>
                        Download in Background
                    </Button>
                </Modal.Footer>
            </Modal>

            {isDownloadInBackground && isDownloading && (
                <div className="minimize-section" style={{ position: 'fixed', bottom: '20px', right: '20px', zIndex: 1050 }}>
                    <div className="minimize-card card shadow" style={{ minWidth: '320px', backgroundColor: '#fff', border: '1px solid #dee2e6', borderRadius: '8px' }}>
                        <div className="card-body p-3">
                            <div className="d-flex align-items-center gap-3">
                                <div className="img-wrapper">
                                    <i className="fas fa-download download-icon text-primary" style={{ fontSize: '20px' }}></i>
                                </div>
                                <div className="alert-content flex-grow-1">
                                    <h6 className="mb-1" style={{ fontSize: '14px', fontWeight: 600 }}>Download Offline is Inprogress</h6>
                                    <div className="progress" style={{ height: '6px' }}>
                                        <div
                                            className="progress-bar progress-bar-striped progress-bar-animated bg-primary"
                                            role="progressbar"
                                            style={{ width: "100%" }}
                                        />
                                    </div>
                                </div>
                            </div>
                            <div className="actions d-flex justify-content-end gap-2 mt-2">
                                <button type="button" className="btn btn-sm btn-light" onClick={handleStopDropdownDownload}> No, Cancel </button>
                                <button type="button" className="btn btn-sm btn-light" onClick={() => setIsDownloadInBackground(false)}>
                                    <i className="fas fa-expand"></i>
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            <SyncConfirmModal
                project={syncConfirmProject ? { id: syncConfirmProject.id!, name: syncConfirmProject.name } : null}
                onClose={() => setSyncConfirmProject(null)}
                onSuccess={async () => {
                    await loadProjects();
                }}
            />

            {/* Download Success Modal */}
            <Modal
                show={downloadSuccessModal}
                onHide={() => setDownloadSuccessModal(false)}
                centered
            >
                <Modal.Header closeButton>
                    <Modal.Title>Success</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    <p className="mb-0">
                        Download completed successfully
                    </p>
                </Modal.Body>
                <Modal.Footer>
                    <Button variant={BUTTONS.PRIMARY} onClick={() => setDownloadSuccessModal(false)}>
                        Ok
                    </Button>
                </Modal.Footer>
            </Modal>

            {/* Error Modal */}
            <Modal
                show={Boolean(transferError)}
                onHide={() => setTransferError(null)}
                centered
            >
                <Modal.Header closeButton>
                    <Modal.Title>Error</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    <p className="mb-0">
                        {transferError || "Unable to Sync/Download due to a very low internet connection. Please proceed when your internet connection is having at least 10 mbps download and upload speed."}
                    </p>
                </Modal.Body>
                <Modal.Footer>
                    <Button variant={BUTTONS.SECONDARY} onClick={() => setTransferError(null)}>
                        Ok
                    </Button>
                </Modal.Footer>
            </Modal>
        </>
    );
};

export default ProjectList;
