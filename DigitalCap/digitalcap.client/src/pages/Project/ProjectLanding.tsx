import axios from 'axios';
import React, { useState, useEffect } from 'react';
import { Spinner } from "react-bootstrap";
import * as Icon from "react-bootstrap-icons";
import * as projectService from '../../service/ProjectService';
import type { ProjectLifecycleAction, SynchedOnlineContact } from '../../service/ProjectService';
import { usePopup } from '../../Common/PopupProvider';
import { resultMessage } from '../../Common/UtilityMessage';
import moment from 'moment';
import { getRelativeTime } from '../../utils/dateTime';
import { TextField, Tab, Button, BUTTONS } from "customer_portal-ui-shared";
import ActivityLog from '../Activity/ActivityLog';
import Comments from '../Activity/Comments';
import ReportDetails from '../SurveyReport/ReportDetails';
import WorkflowDetails from '../Workflow/WorkflowDetails';
import RevenueList from '../Invoice/RevenueList';
import DocumentList from '../File/DocumentList';
import { Link } from 'react-router-dom';
import { isElectron } from '../../utils/runtime';

interface Project {
    name: string;
    imo: string;
    date: string;
    pct: number;
    status: string;
    statusColor?: string;
    id?: number;
    wcn?: string;
    capRegion?: string;
    pidNumber?: string;
    capType?: string;
    capScope?: string;
    statusName?: string;
    projectStatusName?: string;
    projectStatusColor?: string;
    createdDate?: string;
    createdBy?: string;
    lastModifiedDate?: string;
    lastModifiedBy?: string;
    isReadOnlyEnabled?: boolean;
}

interface ProjectLandingProps {
    project: Project;
    onBack: () => void;
    onEdit?: (projectId: number) => void;
    onProjectsChanged?: () => void;
    globalPermission: (perm: string) => boolean;
    initialTab?: number;
}
function isGridOfflineDownloadStatus(project: Project): boolean {
    return String(project.status ?? "").toLowerCase() === "offline";
}
interface ProjectDetail {
    created_on: string;
    created_by: string;
    imo_number: string;
    wcn_number: string;
    vessel_name: string;
    project_id: string;
    cap_coordinator: string;
    potential_drydock_date: string;
    progress: number;
    cap_type: string;
    cap_scope: string;
    status: string;
    status_color?: string;
    project_status: string;
    project_status_color?: string;
    last_modified_on: string;
    last_modified_by: string;
    vessel_cap_history: string;
}

interface StatusBadgeProps {
    status: string;
    type?: string;
}

const FieldBlock: React.FC<{ label: string; value: React.ReactNode; className?: string }> = ({ label, value, className }) => (
    <>
        <TextField label={label} className={className}>
            {value}
        </TextField>
    </>
);

const StatusBadge: React.FC<StatusBadgeProps> = ({
    status,
    type = "status",
}) => (
    <span
        className={`project-status-badge ${status.toLowerCase().replace(/\s+/g, "-")}`}
        data-type={type}
    >
        {status}
    </span>
);

const ProgressBar: React.FC<{ pct: number; status?: string }> = ({ pct, status }) => {
    const getProgressColor = () => {
        if (pct === 100) return "#28a745";
        const s = String(status ?? "").toLowerCase().replace(/[\s_-]+/g, "");
        if (s === "cancelled") return "#6c757d";
        return "#fd7e14";
    };

    return (
        <div className="project-progress-wrap">
            <div className="project-progress-track">
                <div
                    className="project-progress-fill"
                    style={{ width: `${pct}%`, backgroundColor: getProgressColor() }}
                />
            </div>
            <span className="project-progress-pct">{pct}%</span>
        </div>
    );
};

function normalizeStatusKey(status: unknown): string {
    return String(status ?? "")
        .toLowerCase()
        .replace(/[\s_-]+/g, "");
}
function isReadyForClosureStatus(status: unknown): boolean {
    const s = normalizeStatusKey(status);
    return s === "readyforclosure" || s.includes("readyforclosure");
}


function isClosedStatus(status: unknown): boolean {
    const s = normalizeStatusKey(status);
    if (!s || s === "n/a") return false;
    if (isReadyForClosureStatus(status)) return false;
    return s === "closed" || s.includes("closed");
}

function pickProjectStatusDisplay(
    apiData: Record<string, unknown> | undefined,
    project: Project
): string {
    if (!apiData || typeof apiData !== "object") {
        return String(
            project.projectStatusName ?? project.statusName ?? project.status ?? "N/A"
        );
    }
    const raw = apiData.projectStatusName
    if (raw != null && String(raw).trim() !== "") {
        return String(raw);
    }
    return String(
        project.projectStatusName ?? "N/A"
    );
}

function isElectronShell(): boolean {
    return isElectron();
}

const ProjectLanding: React.FC<ProjectLandingProps> = ({ project, onBack, onEdit, onProjectsChanged, globalPermission, initialTab = 0 }) => {
    const popup = usePopup();
    const [detail, setDetail] = useState<ProjectDetail | null>(null);
    const [loading, setLoading] = useState<boolean>(false);
    const [activeTab, setActiveTab] = React.useState<number>(initialTab);
    const [detailReloadNonce, setDetailReloadNonce] = useState(0);
    const [synchedOnlineContact, setSynchedOnlineContact] = useState<SynchedOnlineContact | null>(null);
    const [showDiscardOfflineWarning, setShowDiscardOfflineWarning] = useState(false);
    const [activities, setActivities] = useState<any[]>([]);

    useEffect(() => {
        loadRecentActivities();
    }, [project.id]);

    useEffect(() => {
        const handleRefresh = () => {
            setDetailReloadNonce((n) => n + 1);
        };
        window.addEventListener('refreshProjectList', handleRefresh);
        return () => {
            window.removeEventListener('refreshProjectList', handleRefresh);
        };
    }, []);

    const loadRecentActivities = async () => {
        try {
            const response = await projectService.GetRecentActivities2ByProjectId(false, project.id);

            const mappedActivities = (response.data || []).map((item: any) => ({
                id: item.id,
                ProjectId: item.projectId,
                VesselProject: item.vesselProject,
                text: item.text,
                time: getRelativeTime(item.changeTime),
                userName: item.changedBy,
                userEmail: item.changedBy,
                type: "modify",
                tag: ""
            }));

            setActivities(mappedActivities);
        }
        catch (err) {
        }
    };


    useEffect(() => {
        if (initialTab === 99) {
            const tabsCount = [
                true,
                true,
                true,
                globalPermission("AbsProjectLandingPageRevenueTracker"),
                true,
                true
            ].filter(Boolean).length;
            setActiveTab(tabsCount - 1);
        } else {
            setActiveTab(initialTab);
        }
    }, [initialTab, globalPermission]);


    useEffect(() => {
        const loadProjectDetails = async () => {
            if (project && project.id) {
                setLoading(true);
                try {
                    const response = await projectService.getProjectById(project.id);
                    setSynchedOnlineContact(
                        projectService.parseSynchedOnlineFromLanding(response.data)
                    );

                    if (response.data && response.data.projectDetails) {
                        const apiData = response.data.projectDetails;

                        const getCapTypeString = (capType: number | string | undefined): string => {
                            if (typeof capType === 'string') return capType;
                            switch (capType) {
                                case 0:
                                case 1:
                                    return "Initial Assessment";
                                case 2:
                                    return "Initial Assessment Sister";
                                case 3:
                                    return "Reassessment";
                                default:
                                    return "N/A";
                            }
                        };

                        const projectDetail: ProjectDetail = {
                            created_on: apiData.createdDate
                                ? moment(apiData.createdDate).format('DD-MMM-YYYY')
                                : 'N/A',
                            created_by: apiData.createdBy || project.createdBy || 'N/A',
                            imo_number: project.imo || (apiData.imo ? String(apiData.imo) : 'N/A'),
                            wcn_number: project.wcn || apiData.wcn || 'N/A',
                            vessel_name: apiData.vesselName || 'N/A',
                            project_id: project.pidNumber || apiData.pidNumber || 'N/A',
                            cap_coordinator: project.capRegion || apiData.capRegion || 'N/A',
                            potential_drydock_date: project.date || apiData.potentialDrydockDate || 'N/A',
                            progress: project.pct || response.data.percentComplete || 0,
                            cap_type: getCapTypeString(apiData.capType),
                            cap_scope: project.capScope || apiData.capScope || 'N/A',
                            status: project.statusName || project.status || 'N/A',
                            status_color: apiData.statusColor || apiData.StatusColor,
                            project_status: pickProjectStatusDisplay(
                                apiData as Record<string, unknown>,
                                project
                            ),
                            project_status_color: apiData.projectStatusColor || apiData.ProjectStatusColor,
                            last_modified_on: project.lastModifiedDate
                                ? moment(project.lastModifiedDate).format('DD-MMM-YYYY')
                                : (apiData.statusChangedDate
                                    ? moment(apiData.statusChangedDate).format('DD-MMM-YYYY')
                                    : 'N/A'),
                            last_modified_by: project.lastModifiedBy || apiData.lastModifiedBy || apiData.statusChangedBy || '',
                            vessel_cap_history: 'N/A'
                        };
                        setDetail(projectDetail);
                    } else {
                        const projectDetail: ProjectDetail = {
                            created_on: project.createdDate
                                ? moment(project.createdDate).format('DD-MMM-YYYY')
                                : 'N/A',
                            created_by: project.createdBy || 'N/A',
                            imo_number: project.imo || 'N/A',
                            wcn_number: project.wcn || 'N/A',
                            vessel_name: 'N/A',
                            project_id: project.pidNumber ? `#${project.pidNumber}` : 'N/A',
                            cap_coordinator: project.capRegion || 'N/A',
                            potential_drydock_date: project.date || 'N/A',
                            progress: project.pct || 0,
                            cap_type: project.capType || 'N/A',
                            cap_scope: project.capScope || 'N/A',
                            status: project.statusName || project.status || 'N/A',
                            status_color: undefined,
                            project_status: pickProjectStatusDisplay(undefined, project),
                            project_status_color: undefined,
                            last_modified_on: project.lastModifiedDate
                                ? moment(project.lastModifiedDate).format('DD-MMM-YYYY')
                                : 'N/A',
                            last_modified_by: project.lastModifiedBy || '',
                            vessel_cap_history: 'N/A'
                        };
                        setDetail(projectDetail);
                    }
                } catch (error) {
                    setSynchedOnlineContact(null);
                    const projectDetail: ProjectDetail = {
                        created_on: project.createdDate
                            ? moment(project.createdDate).format('DD-MMM-YYYY')
                            : 'N/A',
                        created_by: project.createdBy || 'N/A',
                        imo_number: project.imo || 'N/A',
                        wcn_number: project.wcn || 'N/A',
                        vessel_name: 'N/A',
                        project_id: project.pidNumber ? `#${project.pidNumber}` : 'N/A',
                        cap_coordinator: project.capRegion || 'N/A',
                        potential_drydock_date: project.date || 'N/A',
                        progress: project.pct || 0,
                        cap_type: project.capType || 'N/A',
                        cap_scope: project.capScope || 'N/A',
                        status: project.statusName || project.status || 'N/A',
                        status_color: undefined,
                        project_status: pickProjectStatusDisplay(undefined, project),
                        project_status_color: undefined,
                        last_modified_on: project.lastModifiedDate
                            ? moment(project.lastModifiedDate).format('DD-MMM-YYYY')
                            : 'N/A',
                        last_modified_by: project.lastModifiedBy || '',
                        vessel_cap_history: 'N/A'
                    };
                    setDetail(projectDetail);
                } finally {
                    setLoading(false);
                }
            }
        };

        loadProjectDetails();
    }, [project, detailReloadNonce]);

    useEffect(() => {
        if (!isElectronShell() || project.id == null || loading) {
            return;
        }

        const isLocalReleased = Boolean(project.isReadOnlyEnabled || (detail as any)?.isReadOnlyEnabled || (detail as any)?.isRleased);
        if (isLocalReleased) {
            setShowDiscardOfflineWarning(true);
            return;
        }

        if (navigator.onLine) {
            let cancelled = false;
            void (async () => {
                try {
                    const res = await axios.get(`/api/DataTransferAPI/GetDownloadedData?projectId=${project.id}`);
                    if (!cancelled) {
                        if (res.data == null || res.data === '' || (typeof res.data === 'object' && Object.keys(res.data).length === 0)) {
                            setShowDiscardOfflineWarning(true);
                            await axios.post(`/api/Offline/UpdateReleasedStatus?projectId=${project.id}`);
                        } else {
                            setShowDiscardOfflineWarning(false);
                        }
                    }
                } catch {
                    if (!cancelled) {
                        setShowDiscardOfflineWarning(false);
                    }
                }
            })();
            return () => {
                cancelled = true;
            };
        } else {
            setShowDiscardOfflineWarning(false);
        }
    }, [project.id, project.isReadOnlyEnabled, (detail as any)?.isReadOnlyEnabled, loading, detailReloadNonce]);

    const confirmLifecycleAction = async (action: ProjectLifecycleAction) => {
        if (!project.id) return;
        const title = action === "close"
            ? "Close Project"
            : action === "cancel"
                ? "Cancel Project"
                : "Delete Project";
        const message = action === "close"
            ? "Are you sure you want to proceed with closing this project?"
            : action === "cancel"
                ? "Are you sure you want to proceed with cancelling this project?"
                : "Are you sure you want to proceed with deleting this project?";

        const isDelete = action === "delete";

        const confirmed = await popup.confirm({
            title,
            message,
            confirmText: isDelete ? "Delete" : "Yes, Proceed",
            danger: isDelete
        });
        if (!confirmed) return;

        try {
            await projectService.postProjectLifecycleAction(action, project.id);
            const actionText = action === "close" ? "Closed" : action === "cancel" ? "Cancelled" : "Deleted";
            await popup.success(resultMessage(project.name || "Project", actionText as any), title);
            onProjectsChanged?.();
            if (action === "delete") {
                onBack();
            } else {
                setDetailReloadNonce((n) => n + 1);
            }
        } catch (e: any) {
            popup.error({ message: e?.message || `Failed to ${action} project.` });
        }
    };

    if (loading) {
        return (
            <div className="bg-white rounded shadow-sm p-5 d-flex flex-column justify-content-center align-items-center project-landing-empty">
                <Spinner animation="border" role="status" variant="primary">
                    <span className="visually-hidden">Loading...</span>
                </Spinner>
                <div className="mt-3 text-muted">Loading project details...</div>
            </div>
        );
    }

    if (!detail) {
        return (
            <div className="bg-white rounded shadow-sm p-5 text-center">
                <div className="text-muted">No project details available</div>
            </div>
        );
    }
    const handleTabClick = (tabIndex: number) => {
        setActiveTab(tabIndex);
    };

    const ps = detail.project_status;
    const isReadyForClosure = isReadyForClosureStatus(ps);
    const isClosed = isClosedStatus(ps);
    const isNormalStatus = !isReadyForClosure && !isClosed;

    return (
        <>
            <div className="row">
                <div className="col-md-12">
                    {((!isElectronShell() && synchedOnlineContact && isGridOfflineDownloadStatus(project)) || showDiscardOfflineWarning) && (
                        <>
                            {!isElectronShell() &&
                                synchedOnlineContact &&
                                isGridOfflineDownloadStatus(project) && (
                                    <div className="alert alert-danger mb-2" role="alert">
                                        <ul className="mb-0 ps-3">
                                            <li>
                                                {project.name} - Report sections are Read-Only all parts have been downloaded for
                                                offline.
                                            </li>
                                            <li>
                                                Contact the following CAP Surveyors if need any details: {synchedOnlineContact.name}{" "}
                                                ({synchedOnlineContact.email})
                                            </li>
                                        </ul>
                                    </div>
                                )}


                            {showDiscardOfflineWarning && (
                                <div className="alert alert-danger mb-2 project-landing-alert" role="alert">
                                    <ul className="mb-0">
                                        <li>
                                            <p className="mb-0">
                                                The CAP Administrator has discarded your offline project. Any changes made in this
                                                project will not be synchronized with the online application.
                                            </p>
                                        </li>
                                    </ul>
                                </div>
                            )}
                        </>
                    )}
                </div>
                <div className="col-md-12">
                    <Tab
                        activeTab={activeTab}
                        onTabChange={handleTabClick}
                        tabData={[
                            {
                                content: (
                                    <>
                                        <div className="project-header">
                                            <div className="section-title mb-3">
                                                <div className="d-flex flex-row gap-3">
                                                    <div className="d-flex flex-column">

                                                        <h5>  {project.name}</h5>
                                                        <span className="project-tagline">
                                                            Created on {detail.created_on} by {detail.created_by}
                                                        </span>
                                                    </div>
                                                </div>
                                                <div className="section-actions d-flex flex-wrap align-items-center gap-2">

                                                    {/* Create Reassessment moved to Overview */}

                                                    {!isElectron() && isReadyForClosure && globalPermission("AbsProjectLandingPageCloseButton") && (
                                                        <Button onClick={() => confirmLifecycleAction("close")}>
                                                            Close Project
                                                        </Button>
                                                    )}

                                                    {/* Cancel Project Button moved to Overview */}

                                                    {/* Edit Button */}
                                                    {!isElectron() && isNormalStatus && globalPermission("AbsProjectLandingPageReassessmentButton") && (
                                                        <Button
                                                            variant={BUTTONS.TABLEDOWNLOAD}
                                                            startIcon={<Icon.Pencil />}
                                                            onClick={() => {
                                                                if (project.id && onEdit) {
                                                                    onEdit(project.id);
                                                                }
                                                            }}
                                                        />
                                                    )}

                                                    {/* Delete Button */}
                                                    {!isElectron() && isNormalStatus && !isGridOfflineDownloadStatus(project) && (
                                                        <Button
                                                            variant={BUTTONS.TABLEDOWNLOAD}
                                                            startIcon={<Icon.Trash />}
                                                            onClick={() => confirmLifecycleAction("delete")}
                                                            aria-label="Delete project"
                                                        />
                                                    )}
                                                </div>
                                            </div>
                                        </div>

                                        <div className="project-landing-row mt-3">
                                            <FieldBlock label="IMO / WCN Number" value={`${detail.imo_number} / ${detail.wcn_number}`} />
                                            <FieldBlock label="Vessel Name" value={detail.vessel_name} />
                                            <FieldBlock label="Project ID" value={detail.project_id} />
                                            <FieldBlock label="CAP Coordinator" value={detail.cap_coordinator} />
                                            <FieldBlock label="Potential Drydock Date" value={detail.potential_drydock_date && detail.potential_drydock_date !== 'N/A' ? moment(detail.potential_drydock_date).format('DD-MMM-YYYY') : 'N/A'} />
                                            <FieldBlock label="Progress" value={<ProgressBar pct={detail.progress} status={detail.project_status} />} />
                                        </div>
                                        <div className="project-landing-divider" />
                                        <div className="project-landing-row">
                                            <FieldBlock label="CAP Type" value={detail.cap_type} />
                                            <FieldBlock label="CAP Scope" value={detail.cap_scope} />
                                            <FieldBlock label="Project Status" value={<StatusBadge status={detail.project_status} type="projectStatus" />} />
                                            <FieldBlock label="Status" value={<StatusBadge status={detail.status} type="status" />} />
                                            {
                                                (() => {
                                                    const isGuid = (val?: string) => Boolean(val && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(val.trim()));
                                                    const displayModifiedBy = (!detail.last_modified_by || isGuid(detail.last_modified_by) || detail.last_modified_by === 'N/A') ? '-' : detail.last_modified_by;
                                                    return <FieldBlock label="Last Modified on/by" value={`${detail.last_modified_on && detail.last_modified_on !== 'N/A' ? moment(detail.last_modified_on).format('DD-MMM-YYYY') : 'N/A'} / ${displayModifiedBy}`} />;
                                                })()
                                            }
                                            <FieldBlock label="Vessel CAP History" value={detail.vessel_cap_history} />
                                        </div>
                                        {!isElectron() && <div className="project-landing-divider" />}
                                        {!isElectron() && project.id != null ? <Comments projectId={project.id} globalpermission={globalPermission("AbsProjectLandingPageManageMyComments")} activeTab={activeTab} /> : null}
                                    </>
                                ),
                                header: 'Project Info'
                            },
                            {
                                content: (
                                    <>
                                        <ReportDetails projectId={project.id} globalPermission={globalPermission} />
                                    </>
                                ),
                                header: 'Report Details'
                            },
                            {
                                content: (
                                    <>
                                        <WorkflowDetails projectId={project.id} />
                                    </>
                                ),
                                header: 'Workflow Details'
                            },
                            !isElectron() && globalPermission("AbsProjectLandingPageRevenueTracker") && {
                                content: (
                                    <RevenueList projectId={project.id} />
                                ),
                                header: 'Revenue Tracker'
                            },
                            {
                                content: (
                                    <>
                                        <DocumentList projectId={project.id} />
                                    </>
                                ),
                                header: 'Project Files'
                            },
                            {
                                content: (
                                    <>
                                        <ActivityLog projectId={project.id} source={"projectLanding"} activities={activities} />
                                        {!isElectron() && (
                                            <div className="text-center mt-3">
                                                <Link className="text-primary text-decoration-none fw-medium" to="/app/Activity/ActivityLog" state={{ projectId: project.id, projectName: project.name }}>
                                                    View All Recent Activity &rarr;
                                                </Link>
                                            </div>
                                        )}
                                    </>
                                ),
                                header: 'Project Activity'
                            }
                        ].filter(Boolean)}
                    />
                </div>
            </div>
        </>
    );
};

export default React.memo(ProjectLanding); 
