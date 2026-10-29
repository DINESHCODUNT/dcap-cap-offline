import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { Button, AbsReactTable, BUTTONS, Divider, Input, AbsSelect, AbsSearch, RadioInput, Label } from "customer_portal-ui-shared";
import * as Icon from 'react-bootstrap-icons';
import ManageTankTemplateList from './ManageTankTemplateList';
import { Modal, Offcanvas, Spinner } from 'react-bootstrap';
import * as tankService from '../../../../service/TankService';
import * as gradingService from '../../../../service/GradingService';
import InformationPopup from '../../../../Common/InformationPopup';
import { isElectron } from '../../../../utils/runtime';

interface ManageTankRow {
    tankId?: string;
    templateId?: number;
    vesselType: string;
    tankType: string;
    tankTypeId?: number | string;
    status: any;
    statusText?: string;
    tankName: string;
    subheader: string;
    imo?: string;
}

type TankFormState = {
    tankName: string;
    subheader: string;

    vesselValue: string | number | null;
    vesselText: string;

    vesselNameValue: string | number | null;
    vesselNameText: string;

    tankTypeValue: string | number | null;
    tankTypeText: string;

    status: boolean;
};

type SelectOption = {
    text: string;
    value: number | string;
};
const ManageTankList: React.FC = () => {
    const [showMobileSidebar, setShowMobileSidebar] = useState(false);
    const [val, updateVal] = React.useState<string>("");
    const [searchApplied, updateSearchApplied] = React.useState<string | null>(null);
    const [open, setOpen] = React.useState<boolean>();
    const [deleteModel, setDeleteModel] = React.useState<boolean>();
    const [statusModel, setStatusModel] = React.useState<boolean>();
    const [tankList, setTankList] = useState<ManageTankRow[]>([]);
    const [loading, setLoading] = useState<boolean>(false);
    const [TankFilter, setTankFilter] = useState<ManageTankRow[]>([]);
    const [dataCount, setDataCount] = useState(0);
    const [selectedIds, setSelectedIds] = useState<string[]>([]);
    const [bulkStatus, setBulkStatus] = useState<boolean>(true);
    const [selectedTankNames, setSelectedTankNames] = useState<string[]>([]);
    const [hierarchicalFilters, setHierarchicalFilters] = useState<{
        vesselType?: string;
        tankName?: string;
        tankType?: string;
    }>({});
    const [formData, setFormData] = useState<TankFormState>({
        tankName: "",
        subheader: "",
        vesselValue: null,
        vesselText: "",
        vesselNameValue: null,
        vesselNameText: "",
        tankTypeValue: null,
        tankTypeText: "",
        status: true
    });

    const [vesselOptions, setVesselOptions] = useState<SelectOption[]>([]);
    const [tankOptions, setTankOptions] = useState<SelectOption[]>([]);
    const [editingTankId, setEditingTankId] = useState<string | null>(null);
    const [error, setError] = React.useState<boolean>();
    const [deletingTank, setDeletingTank] = useState<ManageTankRow | null>(null);
    const [showInfoPopup, setShowInfoPopup] = useState(false);
    const [popupPageName, setPopupPageName] = useState<string | undefined>();
    const initialFilterState = {
        resetPageIndex: false,
        expandAll: false,
        resetFilters: false,
        deselectRows: false,
        filters: {},
        sort: null,
        pageIndex: 0,
        pageSize: "50"
    };

    const getFirstVesselType = (rows: ManageTankRow[]) => {
        for (const row of rows) {
            if (row.vesselType) {
                return row.vesselType;
            }
        }
        return undefined;
    };

    const getPageSizeNum = (pageSize?: string | number) => {
        if (pageSize != null && pageSize !== "") {
            return typeof pageSize === 'string' ? parseInt(pageSize, 10) : Number(pageSize);
        }
        return 50;
    };

    const [appliedFilters, setAppliedFilters] = React.useState({
        ...initialFilterState,
    });
    useEffect(() => {
        loadTankList();
    }, []);

    useEffect(() => {
        gradingService.getVesselType().then(res => {
            setVesselOptions(
                res.data.data.map((v: any) => ({
                    text: v.vesselType,
                    value: v.id
                }))
            );
        });
    }, []);

    const onVesselChange = (vesselType: string) => {
        tankService.GetMappedTankTypes(0, vesselType).then(res => {

            setTankOptions(
                res.data.map((p: any) => ({
                    text: p.tankType,
                    value: p.id
                }))
            );
        });
    };

    const normalizeStatus = (status: any): boolean => {
        if (typeof status === "boolean") return status;
        if (typeof status === "number") return status === 1;
        if (typeof status === "string")
            return status.toLowerCase() === "active" || status === "1" || status === "true";
        return false;
    };

    const handleEditTank = async (row: ManageTankRow) => {
        const isActive = normalizeStatus(row.status);


        setVesselOptions([
            {
                text: row.vesselType,
                value: row.vesselType
            }
        ]);

        const tankTypeId = row.tankTypeId ? Number(row.tankTypeId) : null;

        setTankOptions([
            {
                text: row.tankType,
                value: tankTypeId || row.tankType
            }
        ]);

        setFormData({
            tankName: row.tankName,
            subheader: row.subheader || "",

            vesselValue: row.vesselType,
            vesselText: row.vesselType,

            vesselNameValue: null,
            vesselNameText: "",

            tankTypeValue: tankTypeId || row.tankType,
            tankTypeText: row.tankType,

            status: isActive
        });

        setEditingTankId(row.tankId ?? null);
        setOpen(true);
    };

    const handleSaveTank = async () => {
        if (!formData.tankName.trim() || !formData.vesselText || !formData.tankTypeText.trim()) {
            setError(true);
            return;
        }

        try {
            const tanktypeIdValue = formData.tankTypeValue
                ? (typeof formData.tankTypeValue === 'number'
                    ? formData.tankTypeValue
                    : (typeof formData.tankTypeValue === 'string' && !isNaN(Number(formData.tankTypeValue))
                        ? Number(formData.tankTypeValue)
                        : undefined))
                : undefined;

            const payload = {
                tankName: formData.tankName,
                subheader: formData.subheader || "",
                vesselType: formData.vesselText,
                tankType: formData.tankTypeText,
                tankTypeId: tanktypeIdValue,
                status: formData.status,
                tankId: editingTankId
            };

            if (editingTankId) {
                await tankService.updateTank(payload);

                setPopupPageName("UpdateTank");
            } else {
                await tankService.createTank(payload as any);

                setPopupPageName("CreateTank");
            }

            setShowInfoPopup(true);
            setOpen(false);
            resetForm();
            loadTankList();

        } catch (err) {
        }
    };
    const handleDeleteTank = async () => {
        if (!deletingTank?.tankId) return;

        try {
            await tankService.deleteTank(
                deletingTank.tankId,
                "",
                0
            );

            setDeleteModel(false);
            setDeletingTank(null);

            setPopupPageName("DeleteTank");
            setShowInfoPopup(true);

            await loadTankList();

        } catch (err) {
        }
    };
    const handleBulkStatusUpdate = async () => {

        try {
            const response = await tankService.updateTankStatus({
                data: selectedIds,
                status: bulkStatus,
                IMO: ""
            });
            if (response && response.data) {

                setStatusModel(false);

                setSelectedIds([]);
                setSelectedTankNames([]);
                setBulkStatus(true);

                setPopupPageName("UpdateTankStatus");
                setShowInfoPopup(true);

                await loadTankList();
            }
        } catch (e) {
        }
    };

    const resetForm = () => {
        setEditingTankId(null);
        setError(false);
        setFormData({
            tankName: "",
            subheader: "",
            vesselValue: null,
            vesselText: "",
            vesselNameValue: null,
            vesselNameText: "",
            tankTypeValue: null,
            tankTypeText: "",
            status: true
        });
        setTankOptions([]);

        gradingService.getVesselType().then(res => {
            setVesselOptions(
                res.data.data.map((v: any) => ({
                    text: v.vesselType,
                    value: v.id
                }))
            );
        });
    };

    const loadTankList = async () => {
        try {
            setLoading(true);

            const res = await tankService.getTanks();

            let rawData: any[] = [];

            if (Array.isArray(res.data)) {
                rawData = res.data;
            } else if (Array.isArray(res.data?.data)) {
                rawData = res.data.data;
            } else if (Array.isArray(res.data?.Data)) {
                rawData = res.data.Data;
            }

            const mappedData: ManageTankRow[] = rawData.map(item => {
                const rawStatus = item.status || "N/A";
                const isActive = rawStatus === true || rawStatus === 1 || String(rawStatus).toLowerCase() === 'active' || String(rawStatus).toLowerCase() === 'true';

                return {
                    tankId: item.tankId ?? item.TankId ?? "",
                    vesselType: item.vesselType ?? item.VesselType ?? "",
                    tankType: item.tankType ?? item.TankType ?? "",
                    tankTypeId: item.tankTypeId ?? item.TankTypeId ?? "",
                    status: rawStatus,
                    statusText: isActive ? "Active" : "Inactive",
                    tankName: item.tankName ?? item.TankName ?? "",
                    subheader: item.subheader ?? item.Subheader ?? item.subHeader ?? "",
                    imo: String(item.imo ?? item.IMO ?? item.imoNumber ?? item.IMONumber ?? ""),
                };
            });

            setTankFilter(mappedData);

            const pageSize = getPageSizeNum(appliedFilters.pageSize);
            const pageIndex = appliedFilters.pageIndex || 0;

            const activeHierarchical = hierarchicalFilters.vesselType
                ? hierarchicalFilters
                : (() => {
                    const firstVesselType = getFirstVesselType(mappedData);
                    return firstVesselType ? { vesselType: firstVesselType } : {};
                })();

            if (!hierarchicalFilters.vesselType && activeHierarchical.vesselType) {
                setHierarchicalFilters(activeHierarchical);
            }

            searchVessels(
                { filters: appliedFilters.filters },
                pageIndex,
                pageSize,
                searchApplied,
                activeHierarchical,
                mappedData
            );

        } catch (error) {
        } finally {
            setLoading(false);
        }
    };
    const handleClearFilter = () => {
        updateVal("");
        updateSearchApplied(null);

        const firstVesselType = getFirstVesselType(TankFilter);
        const resetHierarchical = firstVesselType ? { vesselType: firstVesselType } : {};
        setHierarchicalFilters(resetHierarchical);

        setAppliedFilters(prev => ({
            ...prev,
            pageIndex: 0,
            resetPageIndex: true,
            filters: {},
        }));

        if (TankFilter && TankFilter.length > 0) {
            const pagesize = getPageSizeNum(appliedFilters.pageSize);
            searchVessels({ filters: {} },
                0, pagesize, null, resetHierarchical);
        }
    };
    const cleanString = (str: string) =>
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
        data: ManageTankRow[],
        activeFilters: Record<string, unknown>
    ) => {
        const filterEntries = Object.entries(activeFilters);
        if (filterEntries.length === 0) {
            return data;
        }

        return data.filter((row) =>
            filterEntries.every(([columnId, filter]) => {
                if (!filter) return true;

                const { searchTerm, selectedFilters, isFiltersApplied } = filter as any;

                if (!isFiltersApplied) return true;

                if (selectedFilters && selectedFilters.length > 0) {
                    const rowValue = (row as any)[columnId];
                    if (typeof rowValue === 'boolean') {
                        return selectedFilters.some((f: any) => {
                            const fStr = String(f).toLowerCase();
                            if (fStr === 'active' || fStr === 'true' || fStr === 'yes') {
                                return rowValue === true;
                            }
                            if (fStr === 'inactive' || fStr === 'false' || fStr === 'no') {
                                return rowValue === false;
                            }
                            return false;
                        });
                    }
                    return selectedFilters.some((f: any) =>
                        cleanString(String(f)) === cleanString(String(rowValue ?? ''))
                    );
                }

                if (searchTerm && searchTerm.trim() !== '') {
                    const rowValue = cleanString((row as any)[columnId] ?? '');
                    return rowValue.includes(cleanString(searchTerm));
                }

                return true;
            })
        );
    };

    const searchVessels = useCallback((
        filters: any,
        pageIndex: number,
        pageSize: any,
        searchText?: string | null,
        hierarchicalOverride?: typeof hierarchicalFilters,
        dataOverride?: ManageTankRow[]
    ) => {
        const pageSizeNum = getPageSizeNum(pageSize);
        const pageIndexNum = typeof pageIndex === 'string' ? parseInt(pageIndex, 10) : Number(pageIndex);

        const currentSearchText = searchText !== undefined ? searchText : searchApplied;
        const activeHierarchical = hierarchicalOverride ?? hierarchicalFilters;

        let dataToPaginate = dataOverride ?? TankFilter;

        if (activeHierarchical.vesselType) {
            dataToPaginate = dataToPaginate.filter(item =>
                item.vesselType === activeHierarchical.vesselType
            );
        }
        if (activeHierarchical.tankName) {
            dataToPaginate = dataToPaginate.filter(item =>
                item.tankName === activeHierarchical.tankName
            );
        }
        if (activeHierarchical.tankType) {
            dataToPaginate = dataToPaginate.filter(item =>
                item.tankType === activeHierarchical.tankType
            );
        }

        if (currentSearchText) {
            dataToPaginate = dataToPaginate.filter((item) => {
                const searchLower = currentSearchText.toLowerCase();
                return (
                    String(item.tankName || '').toLowerCase().includes(searchLower) ||
                    String(item.tankType || '').toLowerCase().includes(searchLower) ||
                    String(item.vesselType || '').toLowerCase().includes(searchLower)
                );
            });
        }

        const activeFilters = filters?.filters || appliedFilters.filters || {};
        const columnFilteredData = applyColumnFilters(dataToPaginate, activeFilters);

        const startIndex = pageIndexNum * pageSizeNum;
        const endIndex = startIndex + pageSizeNum;
        const filteredPageData = columnFilteredData.slice(startIndex, endIndex);

        setTankList(prev => {
            if (prev.length === filteredPageData.length && JSON.stringify(prev) === JSON.stringify(filteredPageData)) {
                return prev;
            }
            return filteredPageData;
        });
        setDataCount(columnFilteredData.length);
    }, [TankFilter, searchApplied, hierarchicalFilters, appliedFilters]);

    const applyFilter = useCallback(
        (filtersObj: any) => {
            const filteredData = Object.fromEntries(
                Object.entries(filtersObj?.filters || {}).filter(
                    ([, value]) => {
                        if (!value) return false;
                        const v = value as any;
                        return (v.searchTerm && v.searchTerm !== "")
                            || (v.selectedFilters && v.selectedFilters.length > 0)
                            || v.isFiltersApplied;
                    }
                )
            );

            const {
                pageIndex: newPageIndex,
                pageSize: newPageSize,
            } = filtersObj || {};

            const pageSizeNum = getPageSizeNum(newPageSize);

            setAppliedFilters((prev) => {
                const prevFiltersStr = JSON.stringify(prev.filters || {});
                const newFiltersStr = JSON.stringify(filteredData || {});
                const isFilterChanged = prevFiltersStr !== newFiltersStr;

                const pageIndexNum = isFilterChanged
                    ? 0
                    : (typeof newPageIndex === 'string'
                        ? parseInt(newPageIndex, 10)
                        : Number(newPageIndex) || 0);

                const updatedFilters = {
                    ...filtersObj,
                    pageIndex: pageIndexNum,
                    filters: filteredData,
                    sort: filtersObj.sort,
                    searchType: "all",
                    pageSize: String(pageSizeNum),
                    resetPageIndex: isFilterChanged,
                };

                if (JSON.stringify(prev) === JSON.stringify(updatedFilters)) {
                    return prev;
                }

                searchVessels(
                    { ...updatedFilters, filters: filteredData },
                    pageIndexNum,
                    pageSizeNum
                );

                return updatedFilters;
            });
        },
        [searchVessels]
    );

    const searchTimeoutRef = React.useRef<any>(null);

    const onChangeSearchInput = useCallback((value: string) => {
        updateVal(value);

        if (searchTimeoutRef.current) {
            clearTimeout(searchTimeoutRef.current);
        }

        searchTimeoutRef.current = setTimeout(() => {
            if (value.trim() === "") {
                updateSearchApplied(null);
                if (TankFilter && TankFilter.length > 0) {
                    const pagesize = getPageSizeNum(appliedFilters.pageSize);
                    setAppliedFilters(prev => ({
                        ...prev,
                        pageIndex: 0,
                        resetPageIndex: true
                    }));
                    searchVessels({ filters: appliedFilters.filters },
                        0, pagesize, null);
                }
            } else {
                updateSearchApplied(value);
                if (TankFilter && TankFilter.length > 0) {
                    const pagesize = getPageSizeNum(appliedFilters.pageSize);
                    setAppliedFilters(prev => ({
                        ...prev,
                        pageIndex: 0,
                        resetPageIndex: true
                    }));
                    searchVessels({ filters: appliedFilters.filters },
                        0, pagesize, value);
                }
            }
        }, 400);
    }, [TankFilter, appliedFilters.pageSize, appliedFilters.filters, searchVessels]);

    const hierarchicallyFilteredData = useMemo(() => {
        let data = TankFilter;
        if (hierarchicalFilters.vesselType) {
            data = data.filter(item => item.vesselType === hierarchicalFilters.vesselType);
        }
        if (hierarchicalFilters.tankName) {
            data = data.filter(item => item.tankName === hierarchicalFilters.tankName);
        }
        if (hierarchicalFilters.tankType) {
            data = data.filter(item => item.tankType === hierarchicalFilters.tankType);
        }
        return data;
    }, [TankFilter, hierarchicalFilters]);

    const columnUniqueValues = useMemo(() => {
        const filters = appliedFilters.filters || {};

        const uniq = (targetKey: string) => {
            let src = hierarchicallyFilteredData || [];

            if (searchApplied && searchApplied.trim()) {
                const searchLower = searchApplied.toLowerCase().trim();
                src = src.filter((item: any) => {
                    return (
                        String(item.tankName || '').toLowerCase().includes(searchLower) ||
                        String(item.tankType || '').toLowerCase().includes(searchLower) ||
                        String(item.vesselType || '').toLowerCase().includes(searchLower)
                    );
                });
            }

            const filterEntries = Object.entries(filters);
            if (filterEntries.length > 0) {
                src = src.filter((tank: any) =>
                    filterEntries.every(([columnId, filter]) => {
                        if (columnId === targetKey) return true;

                        if (!filter) return true;

                        const { searchTerm, selectedFilters, isFiltersApplied } = filter as any;

                        if (!isFiltersApplied) return true;

                        if (selectedFilters && selectedFilters.length > 0) {
                            const tankValue = tank[columnId];
                            if (typeof tankValue === 'boolean') {
                                return selectedFilters.some((f: any) => {
                                    const fStr = String(f).toLowerCase();
                                    if (fStr === 'active' || fStr === 'true' || fStr === 'yes') return tankValue === true;
                                    if (fStr === 'inactive' || fStr === 'false' || fStr === 'no') return tankValue === false;
                                    return false;
                                });
                            }
                            return selectedFilters.some((f: any) =>
                                cleanString(String(f)) === cleanString(String(tankValue ?? ''))
                            );
                        }

                        if (searchTerm && searchTerm.trim() !== '') {
                            const tankValue = cleanString(tank[columnId] ?? '');
                            return tankValue.includes(cleanString(searchTerm));
                        }

                        return true;
                    })
                );
            }

            const vals = [...new Set(src.map((r: any) => String(r[targetKey] ?? '')).filter(v => v !== ''))];
            return vals.sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }));
        };

        return {
            tankName: uniq('tankName'),
            tankType: uniq('tankType'),
            statusText: uniq('statusText'),
            subheader: uniq('subheader'),
        };
    }, [hierarchicallyFilteredData, searchApplied, appliedFilters.filters]);

    const columns = useMemo(
        () => [
            {
                label: "Tank Name",
                accessorKey: "tankName",
                accessor: "tankName",
                columnValues: { type: "both", data: columnUniqueValues.tankName },
            },
            {
                label: "Subheader",
                accessorKey: "subheader",
                accessor: "subheader",
                columnValues: { type: "both", data: columnUniqueValues.subheader },
            },
            {
                label: "Tank Type",
                accessorKey: "tankType",
                accessor: "tankType",
                columnValues: { type: "both", data: columnUniqueValues.tankType },
            },
            {
                label: "Status",
                accessorKey: "statusText",
                accessor: "statusText",
                columnValues: { type: "both", data: columnUniqueValues.statusText },
            },
            {
                id: "action",
                label: "Action",
                filtering: false,
                sorting: false,
                Cell: ({ row }: any) => (
                    <div className="d-flex gap-3">
                        <Button
                            variant={BUTTONS.TABLEDOWNLOAD}
                            startIcon={<Icon.PencilFill />}
                            onClick={(e: any) => {
                                e.stopPropagation();
                                handleEditTank(row.original);
                            }}
                        />
                        {!isElectron() && (
                            <Button
                                variant={BUTTONS.TABLEDOWNLOAD}
                                startIcon={<Icon.TrashFill />}
                                onClick={(e: any) => {
                                    e.stopPropagation();
                                    setDeletingTank(row.original);
                                    setDeleteModel(true);
                                }}
                            />
                        )}
                    </div>
                )
            }
        ],
        [columnUniqueValues]
    );


    const handleHierarchicalFilterChange = useCallback((filters: {
        vesselType?: string;
        tankType?: string;
        tankName?: string;
    }) => {
        setHierarchicalFilters(filters);
        setAppliedFilters(prev => ({
            ...prev,
            pageIndex: 0,
            resetPageIndex: true,
            filters: {},
        }));

        const pageSize = getPageSizeNum(appliedFilters.pageSize);
        searchVessels({ filters: {} },
            0, pageSize, undefined, filters);
    }, [appliedFilters.pageSize, searchVessels]);
    const tankHeaderTitle = useMemo(() => {
        if (hierarchicalFilters.tankName) {
            return `${hierarchicalFilters.tankName} - Tank`;
        }
        if (hierarchicalFilters.tankType) {
            return `${hierarchicalFilters.tankType} - Tank`;
        }
        if (hierarchicalFilters.vesselType) {
            return `${hierarchicalFilters.vesselType} - Tank`;
        }
        return "Tank";
    }, [hierarchicalFilters]);

    useEffect(() => {
        if (showInfoPopup) {
            const timer = setTimeout(() => {
                setShowInfoPopup(false);
                setPopupPageName(undefined);
            }, 2000);

            return () => clearTimeout(timer);
        }
    }, [showInfoPopup]);

    const handleRowSelected = useCallback((rows: ManageTankRow[]) => {
        if (!rows || !Array.isArray(rows)) return;
        const ids = rows.map((row: any) => row.tankId || row.id || "").filter((id: string) => id !== "");
        const names = rows.map((row: any) => row.tankName || row.tank_name || "").filter((name: string) => name !== "");

        setSelectedIds((prevIds: string[]) => {
            const prevIdsStr = prevIds.slice().sort().join(",");
            const newIdsStr = ids.slice().sort().join(",");
            if (prevIdsStr === newIdsStr) return prevIds;
            return ids;
        });

        setSelectedTankNames((prevNames: string[]) => {
            const prevNamesStr = prevNames.slice().sort().join(",");
            const newNamesStr = names.slice().sort().join(",");
            if (prevNamesStr === newNamesStr) return prevNames;
            return names;
        });
    }, []);

    return (
        <>
            <div className="page-content">
                <div className="grid-container">
                    <Offcanvas show={showMobileSidebar} onHide={() => setShowMobileSidebar(false)} placement="end" className="d-md-none">
                        <Offcanvas.Header closeButton>
                            <Offcanvas.Title>Filters</Offcanvas.Title>
                        </Offcanvas.Header>
                        <Offcanvas.Body>
                            <ManageTankTemplateList
                                data={TankFilter}
                                onFilterChange={handleHierarchicalFilterChange}
                                selectedFilters={hierarchicalFilters}
                            />
                        </Offcanvas.Body>
                    </Offcanvas>
                    <div className="grid-sidebar d-none d-md-block">
                        <ManageTankTemplateList
                            data={TankFilter}
                            onFilterChange={handleHierarchicalFilterChange}
                            selectedFilters={hierarchicalFilters}
                        />
                    </div>
                    <div className="grid-content-body">
                        <div className="row align-items-center mb-3">
                            <div className="col-8 col-md-8">
                                <h5 className='_600 mb-0'>{tankHeaderTitle}</h5>
                            </div>
                            <div className="col-4 d-md-none text-end">
                                <Icon.Funnel 
                                    size={24} 
                                    onClick={() => setShowMobileSidebar(true)} 
                                    style={{ cursor: 'pointer' }} 
                                />
                            </div>
                        </div>
                        <div className="row">
                            <div className="col-md-6 mb-3 mt-2">
                                <div className="d-flex justify-content-start align-items-center gap-2">
                                    <AbsSearch
                                        handleClear={() => {
                                            handleClearFilter();
                                        }}
                                        value={val}
                                        handleChange={(e: any) => onChangeSearchInput(e.target.value)}
                                        handleSearch={() => {
                                            updateSearchApplied(val === "" ? null : val);
                                        }}
                                        placeholder="Search by Tank Name, Tank Type, Vessel Type"
                                        searchApplied={searchApplied !== null}
                                    />
                                    <a onClick={() => handleClearFilter()} href="#" className='text-link text-nowrap _500'>Clear Filters</a>
                                </div>
                            </div>
                            <div className="col-md-6 d-flex justify-content-end gap-2">
                                {!isElectron() && (
                                    <>
                                        <div>
                                            <Button variant={BUTTONS.PRIMARY} onClick={() => {
                                                resetForm();
                                                setOpen(true);
                                            }}>Add New Tank</Button>
                                        </div>
                                        <div>
                                            <Button variant={BUTTONS.PRIMARY} disabled={selectedIds.length === 0}
                                                onClick={() => setStatusModel(true)}>Change Status</Button>
                                        </div>
                                    </>
                                )}
                            </div>
                        </div>
                        <div className="custom-react-table">
                            {loading ? (
                                <div className="d-flex justify-content-center align-items-center" style={{ minHeight: '400px' }}>
                                    <Spinner animation="border" role="status" variant="primary">
                                        <span className="visually-hidden">Loading...</span>
                                    </Spinner>
                                </div>
                            ) : (
                                <AbsReactTable
                                    columns={columns}
                                    data={tankList}
                                    pagination={true}
                                    expand={false}
                                    allowColumnsShowHide={false}
                                    shadow={true}
                                    rowSelect={!isElectron()}
                                    onRowSelected={handleRowSelected}
                                    dataCount={dataCount}
                                    applyFilterCallback={applyFilter}
                                    appliedFilters={{
                                        ...appliedFilters,
                                        pageSize: appliedFilters.pageSize.toString(),
                                        pageIndex: appliedFilters.pageIndex.toString(),
                                    }}
                                    pageOptions={["50", "100", "150", "200"]}
                                    allDataProvided
                                />
                            )}
                        </div>
                    </div>
                </div>
            </div>
            <Offcanvas show={open} placement={"end"} onHide={() => { setOpen(false); resetForm(); }} backdrop="static">
                <Offcanvas.Header closeButton>
                    <Offcanvas.Title>
                        {editingTankId ? "Edit Tank" : "Add New Tank"}
                    </Offcanvas.Title>
                </Offcanvas.Header>
                <Offcanvas.Body>
                    <div className="row">
                        <div className="col-md-12 form-group">
                            <Label name="Tank Name" bold required />
                            <Input
                                placeholder="Enter Tank Name"
                                value={formData.tankName}
                                onChange={(value: string) =>
                                    setFormData(prev => ({ ...prev, tankName: value }))
                                }
                            />
                            {(error && (!formData.tankName || formData.tankName == "" || formData.tankName == null)) ?
                                <div><label style={{ color: 'red' }}> Tank Name Required</label></div> :
                                <></>
                            }
                        </div>
                        <div className="col-md-12 form-group">
                            <Label name="Subheader" bold />
                            <Input
                                placeholder="Enter Subheader Name"
                                value={formData.subheader}
                                onChange={(value: string) =>
                                    setFormData(prev => ({ ...prev, subheader: value }))
                                }
                            />

                        </div>

                        <div className="col-md-12 form-group">
                            <Label name="Select a Vessel Type" bold required />
                            <AbsSelect
                                options={vesselOptions}
                                mult={false}
                                disabled={!!editingTankId}
                                selected={formData.vesselValue ? [formData.vesselValue] : []}
                                onChange={(selected: any[]) => {
                                    const value = selected?.[0];
                                    if (!value) return;

                                    const opt = vesselOptions.find(o => o.value === value);
                                    if (!opt) return;

                                    setFormData(prev => ({
                                        ...prev,
                                        vesselValue: opt.value,
                                        vesselText: opt.text,
                                        tankTypeValue: null,
                                        tankTypeText: ""
                                    }));

                                    onVesselChange(opt.text);
                                }}
                                placeholder="Select a Vessel Type"
                            />
                            {(error && (!formData.vesselValue || formData.vesselValue == null)) ?
                                <div><label style={{ color: 'red' }}> Vessel Type Required</label></div> :
                                <></>
                            }
                        </div>
                        <div className="col-md-12 form-group">
                            <Label name="Tank Type" bold required />
                            <AbsSelect
                                options={tankOptions}
                                mult={false}
                                disabled={!!editingTankId}
                                selected={formData.tankTypeValue ? [formData.tankTypeValue] : []}
                                onChange={(selected: any[]) => {
                                    const value = selected?.[0];
                                    if (!value) return;

                                    const opt = tankOptions.find(o => o.value === value);
                                    if (!opt) return;

                                    setFormData(prev => ({
                                        ...prev,
                                        tankTypeValue: opt.value,
                                        tankTypeText: opt.text
                                    }));
                                }}
                                placeholder="Select a Tank Type"
                            />
                            {(error && (!formData.tankTypeValue || formData.tankTypeValue == null)) ?
                                <div><label style={{ color: 'red' }}> Tank Type Required</label></div> :
                                <></>
                            }
                        </div>
                        <div className="col-md-12 form-group">
                            <Label name="Activation Status" bold />
                            <div className='d-flex justify-contentn-start gap-3'>
                                <RadioInput
                                    label="Active"
                                    checked={formData.status === true}
                                    onChange={() =>
                                        setFormData(prev => ({ ...prev, status: true }))
                                    }
                                />

                                <RadioInput
                                    label="Inactive"
                                    checked={formData.status === false}
                                    onChange={() =>
                                        setFormData(prev => ({ ...prev, status: false }))
                                    }
                                />

                            </div>
                        </div>
                        <div className="col-md-12 form-group">
                            <Divider className="divider-1" />
                        </div>
                        <div className="col-md-12 form-group">
                            <div className="d-flex gap-2">
                                <Button variant={BUTTONS.PRIMARY} onClick={handleSaveTank}>
                                    {editingTankId ? "Update" : "Save"}
                                </Button>

                                <Button variant={BUTTONS.SECONDARY} onClick={() => {
                                    setOpen(false);
                                    resetForm();
                                }}
                                > Cancel</Button>

                            </div>
                        </div>

                    </div>
                </Offcanvas.Body>
            </Offcanvas>
            <Modal show={deleteModel} size="lg" centered onHide={() => setDeleteModel(false)} animation={false} backdrop="static">
                <Modal.Header closeButton>
                    <Modal.Title>Delete Tank</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    <div className="row">
                        <div className="col-md-12">
                            <div className="d-flex justify-content-center align-items-center flex-column">
                                <div className="delete-icon-wrapper mb-4">
                                    <Icon.Trash size={24} />
                                </div>
                                <p className='_500 tx-14'>
                                    Are you sure you want to delete <b> {deletingTank?.tankName}</b> Tank?

                                </p>
                            </div>
                        </div>
                    </div>
                </Modal.Body>
                <Modal.Footer className='gap-3'>
                    <Button variant={BUTTONS.SECONDARY} onClick={() => setDeleteModel(false)}>
                        Cancel
                    </Button>

                    <Button variant={BUTTONS.PRIMARY} onClick={handleDeleteTank}>
                        Delete
                    </Button>

                </Modal.Footer>
            </Modal>
            <Modal show={statusModel} size="lg" centered onHide={() => setStatusModel(false)} animation={false} backdrop="static">
                <Modal.Header closeButton>
                    <Modal.Title>Confirm</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    <div className="row">
                        <div className="col-md-12">
                            <div className="d-flex justify-content-center align-items-center flex-column">
                                <div className="info-icon-wrapper mb-4">
                                    <Icon.Exclamation size={24} />
                                </div>
                                <p className='_500 tx-14 text-center'>
                                    {selectedTankNames.length === 1
                                        ? `Are you sure you want to change '${selectedTankNames[0]}' status?`
                                        : `Are you sure you want to change status of ${selectedTankNames.length} tanks?`}
                                </p>

                                <div className='d-flex jusify-content-center flex-row'>
                                    <RadioInput
                                        label="Active"
                                        checked={bulkStatus === true}
                                        onChange={() => setBulkStatus(true)}
                                    />

                                    <RadioInput
                                        label="Inactive"
                                        checked={bulkStatus === false}
                                        onChange={() => setBulkStatus(false)}
                                    />


                                </div>
                            </div>
                        </div>
                    </div>
                </Modal.Body>
                <Modal.Footer className='gap-3'>
                    <Button variant={BUTTONS.SECONDARY} onClick={() => setStatusModel(false)}>
                        No, Cancel
                    </Button>
                    <Button variant={BUTTONS.PRIMARY} onClick={handleBulkStatusUpdate}>
                        Yes, Proceed
                    </Button>
                </Modal.Footer>
            </Modal>
            {showInfoPopup && (
                <InformationPopup
                    show={showInfoPopup}
                    pageName={popupPageName}
                    onClose={() => {
                        setShowInfoPopup(false);
                        setPopupPageName(undefined);
                    }}
                />)}

        </>
    );
};

export default ManageTankList;