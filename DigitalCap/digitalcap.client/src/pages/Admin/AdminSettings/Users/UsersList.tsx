import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { Button, AbsReactTable, BUTTONS, AbsSearch } from "customer_portal-ui-shared";
import * as Icon from 'react-bootstrap-icons';
import { Modal, Spinner } from 'react-bootstrap';
import { useSearchParams, useNavigate } from 'react-router-dom';
import * as absUserService from '../../../../service/AbsUserService';
import InformationPopup from '../../../../Common/InformationPopup';
import AddUser, { type CapUser } from './AddUser';
import { usePermissions } from '../../../../context/PermissionContext';
import { PERMISSIONS } from '../../../../constants/permissions';

interface CapUsersViewModel {
    capUsers: CapUser[];
    isClientUsers: boolean;
    userCreated: boolean;
    userUpdated: boolean;
    clientId?: string | number;
    company?: string;
}

const UsersList: React.FC = () => {
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();
    const [showAddUser, setShowAddUser] = useState<boolean>(false);
    const [editUser, setEditUser] = useState<CapUser | null>(null);

    const company = searchParams.get('company') || '';
    const clientId = searchParams.get('clientId') || '';
    const userRole = searchParams.get('role') || searchParams.get('userRole') || '';
    const userCreated = searchParams.get('userCreated') === 'true';
    const userUpdated = searchParams.get('userUpdated') === 'true';
    const autoOpen = searchParams.get('autoOpen') === 'true' || (company && userRole);

    const [isClientUsers, setIsClientUsers] = useState(false);
    const [val, updateVal] = useState<string>("");
    const [searchApplied, updateSearchApplied] = useState<string | null>(null);
    const [deleteModel, setDeleteModel] = useState<boolean>(false);
    const [capUsersFilter, setCapUsersFilter] = useState<CapUser[]>([]);
    const { hasGlobalPermission, reloadPermissions } = usePermissions();
    const [paginatedUsers, setPaginatedUsers] = useState<CapUser[]>([]);
    const [loading, setLoading] = useState<boolean>(false);
    const [deletingUser, setDeletingUser] = useState<CapUser | null>(null);
    const [showInfoPopup, setShowInfoPopup] = useState<boolean>(false);
    const [popupPageName, setPopupPageName] = useState<string>("");
    const [dataCount, setDataCount] = useState<number>(0);
    const [responseMsg, setResponseMsg] = useState<string>("");

    const initialFilterState = {
        searchType: "list",
        resetPageIndex: false,
        expandAll: false,
        resetFilters: false,
        deselectRows: false,
        filters: {},
        sort: null,
        pageIndex: 0,
        pageSize: "20"
    };

    const [appliedFilters, setAppliedFilters] = useState({
        ...initialFilterState,
    });

    const hasManageAbsUsersPermission = hasGlobalPermission(PERMISSIONS.MANAGE_ABS_USERS);
    const hasManageClientUsersPermission = hasGlobalPermission(PERMISSIONS.MANAGE_CLIENT_USERS);
    const hasPermission = isClientUsers ? hasManageClientUsersPermission : hasManageAbsUsersPermission;

    useEffect(() => {
        updateVal("");
        updateSearchApplied(null);
        localStorage.removeItem("SearchUsersFilter");

        loadCapUsers();

        if (userCreated) {
            setPopupPageName("CreateUser");
            setShowInfoPopup(true);
            const newSearchParams = new URLSearchParams(searchParams);
            newSearchParams.delete('userCreated');
            navigate({ search: newSearchParams.toString() }, { replace: true });
        } else if (userUpdated) {
            setPopupPageName("UpdateUser");
            setShowInfoPopup(true);
            const newSearchParams = new URLSearchParams(searchParams);
            newSearchParams.delete('userUpdated');
            navigate({ search: newSearchParams.toString() }, { replace: true });
        }
    }, []);

    useEffect(() => {
        if (autoOpen && company && userRole && !showAddUser) {
            setEditUser(null);
            setShowAddUser(true);
        }
    }, [autoOpen, company, userRole, showAddUser]);

    const loadCapUsers = async () => {
        setLoading(true);
        try {
            const response = await absUserService.getCapUsers({
                isClientUsers: isClientUsers,
                clientId: clientId || undefined,
                company: company || undefined
            });

            let viewModel: CapUsersViewModel;
            if (response.data?.data) {
                viewModel = response.data.data;
            } else if (response.data) {
                viewModel = response.data;
            } else {
                viewModel = { capUsers: [], isClientUsers: false, userCreated: false, userUpdated: false };
            }

            setIsClientUsers(viewModel.isClientUsers || false);

            let dataArray: CapUser[] = [];
            if (Array.isArray(viewModel.capUsers)) {
                dataArray = viewModel.capUsers;
            } else if (Array.isArray(response.data)) {
                dataArray = response.data;
            }

            setCapUsersFilter(dataArray);
            setDataCount(dataArray.length);

            if (dataArray.length > 0) {
                const pageSizeNum = typeof appliedFilters.pageSize === 'string'
                    ? parseInt(appliedFilters.pageSize, 10)
                    : Number(appliedFilters.pageSize) || 20;
                searchUsers({}, "list", 0, pageSizeNum, null, dataArray);
            } else {
                setPaginatedUsers([]);
            }
        } catch (error) {
            setCapUsersFilter([]);
            setPaginatedUsers([]);
            setDataCount(0);
        } finally {
            setLoading(false);
        }
    };

    const cleanString = (str: any) =>
        str
            ? str
                .toString()
                .normalize("NFKD")
                .replace(/\s+/g, " ")
                .replace(/[\u200B-\u200D\uFEFF]/g, "")
                .trim()
                .toLowerCase()
            : "";

    const applyColumnFilters = useCallback((
        data: CapUser[],
        activeFilters: Record<string, any>
    ): CapUser[] => {
        const filterEntries = Object.entries(activeFilters || {});
        if (filterEntries.length === 0) return data;

        return data.filter((user) =>
            filterEntries.every(([columnId, filter]) => {
                const { searchTerm, selectedFilters, isFiltersApplied } = filter as any;

                if (!isFiltersApplied) return true;

                if (selectedFilters && selectedFilters.length > 0) {
                    const userValue = (user as any)[columnId];
                    return selectedFilters.some((f: any) =>
                        cleanString(String(f)) === cleanString(String(userValue ?? ''))
                    );
                }

                if (searchTerm && searchTerm.trim() !== '') {
                    const userValue = cleanString((user as any)[columnId] ?? '');
                    return userValue.includes(cleanString(searchTerm));
                }

                return true;
            })
        );
    }, []);

    const searchUsers = useCallback((
        filters: any,
        _searchType: string,
        pageIndex: number,
        pageSize: any,
        searchText?: string | null,
        usersData?: CapUser[]
    ) => {
        const pageSizeNum = typeof pageSize === 'string' ? parseInt(pageSize, 10) : Number(pageSize);
        const pageIndexNum = typeof pageIndex === 'string' ? parseInt(pageIndex, 10) : Number(pageIndex);

        const dataToPaginate = usersData || capUsersFilter;

        const currentSearchText = searchText !== undefined ? searchText : searchApplied;

        let textFiltered = dataToPaginate;
        if (currentSearchText) {
            const searchLower = currentSearchText.toLowerCase().trim();
            textFiltered = dataToPaginate.filter((user) => {
                const firstNameMatch = user.firstName?.toLowerCase().includes(searchLower) || false;
                const lastNameMatch = user.lastName?.toLowerCase().includes(searchLower) || false;
                const userRoleMatch = user.userRole?.toLowerCase().includes(searchLower) || false;
                const fullName = `${user.firstName || ''} ${user.lastName || ''}`.toLowerCase().trim();
                return firstNameMatch || lastNameMatch || userRoleMatch || fullName.includes(searchLower);
            });
        }

        const activeFilters = filters?.filters || {};
        const columnFiltered = applyColumnFilters(textFiltered, activeFilters);

        const startIndex = pageIndexNum * pageSizeNum;
        const endIndex = startIndex + pageSizeNum;
        setPaginatedUsers(columnFiltered.slice(startIndex, endIndex));
        setDataCount(columnFiltered.length);
    }, [capUsersFilter, searchApplied, applyColumnFilters]);

    const handleDeleteConfirm = async () => {
        if (!deletingUser) return;

        setLoading(true);
        try {
            if (isClientUsers) {
                await absUserService.deleteClientUser(deletingUser.aspNetUserId);
            } else {
                await absUserService.deleteAbsUser(deletingUser.aspNetUserId);
            }

            setPopupPageName("DeleteUser");
            setShowInfoPopup(true);
            await loadCapUsers();

        } catch (error) {
        }
        finally {
            setDeleteModel(false);
            setDeletingUser(null);
            setLoading(false);
        }
    };

    const applyFilter = useCallback(
        (filtersObj: any) => {
            const {
                pageIndex: newPageIndex,
                pageSize: newPageSize,
            } = filtersObj;

            const pageSizeNum = typeof newPageSize === 'string'
                ? parseInt(newPageSize, 10)
                : Number(newPageSize) || 20;

            setAppliedFilters((prev) => {
                const prevFiltersStr = JSON.stringify(prev.filters || {});
                const newFiltersStr = JSON.stringify(filtersObj.filters || {});
                const isFilterChanged = prevFiltersStr !== newFiltersStr;

                const pageIndexNum = isFilterChanged
                    ? 0
                    : (typeof newPageIndex === 'string'
                        ? parseInt(newPageIndex, 10)
                        : Number(newPageIndex) || 0);

                const updatedFilters = {
                    ...filtersObj,
                    pageIndex: pageIndexNum,
                    filters: filtersObj.filters || {},
                    sort: filtersObj.sort,
                    searchType: "all",
                    pageSize: String(pageSizeNum),
                    resetPageIndex: isFilterChanged,
                };

                searchUsers(
                    updatedFilters,
                    "all",
                    pageIndexNum,
                    pageSizeNum
                );

                return updatedFilters;
            });
        },
        [searchUsers]
    );

    const onChangeSearchInput = useCallback((value: string) => {
        updateVal(value);

        if (value.trim() === "") {
            updateSearchApplied(null);
            localStorage.removeItem("SearchUsersFilter");
            if (capUsersFilter && capUsersFilter.length > 0) {
                const pagesize = appliedFilters.pageSize != null && appliedFilters.pageSize != ""
                    ? (typeof appliedFilters.pageSize === 'string' ? parseInt(appliedFilters.pageSize, 10) : Number(appliedFilters.pageSize))
                    : 20;
                setAppliedFilters(prev => ({
                    ...prev,
                    pageIndex: 0,
                    resetPageIndex: true
                }));
                searchUsers({}, "list", 0, pagesize, null);
                setDataCount(capUsersFilter.length);
            }
        } else {
            updateSearchApplied(value);
            localStorage.setItem("SearchUsersFilter", value);
            if (capUsersFilter && capUsersFilter.length > 0) {
                const pagesize = appliedFilters.pageSize != null && appliedFilters.pageSize != ""
                    ? (typeof appliedFilters.pageSize === 'string' ? parseInt(appliedFilters.pageSize, 10) : Number(appliedFilters.pageSize))
                    : 20;
                setAppliedFilters(prev => ({
                    ...prev,
                    pageIndex: 0,
                    resetPageIndex: true
                }));
                searchUsers({}, "list", 0, pagesize, value);
            }
        }
    }, [capUsersFilter, appliedFilters.pageSize, searchUsers]);

    useEffect(() => {
        if (capUsersFilter && capUsersFilter.length > 0) {
            if (paginatedUsers.length === 0) {
                const pagesize = appliedFilters.pageSize != null && appliedFilters.pageSize != ""
                    ? (typeof appliedFilters.pageSize === 'string' ? parseInt(appliedFilters.pageSize, 10) : Number(appliedFilters.pageSize))
                    : 20;

                if (searchApplied) {
                    searchUsers({}, "list", 0, pagesize, searchApplied);
                } else {
                    searchUsers({}, "list", 0, pagesize);
                }
                setDataCount(capUsersFilter.length);
            }
        }
    }, [capUsersFilter, searchApplied]);

    const handleEditUser = useCallback((row: CapUser) => {
        setEditUser(row);
        setShowAddUser(true);
    }, []);

    const handleDeleteClickMemo = useMemo(() => (user: CapUser) => {
        setDeletingUser(user);
        setDeleteModel(true);
    }, []);

    const handleAddUserSaved = async (action: "CreateUser" | "UpdateUser") => {
        setPopupPageName(action);
        setShowInfoPopup(true);
        setEditUser(null);
        await loadCapUsers();
        await reloadPermissions();
    };

    const handleAddUserError = (message: string) => {
        setResponseMsg(message);
        setPopupPageName("Manage ABS Users");
        setShowInfoPopup(true);
    };

    const handleAddUserHide = () => {
        setShowAddUser(false);
        setEditUser(null);
    };

    const handleClearFilter = useCallback(() => {
        updateVal("");
        updateSearchApplied(null);
        localStorage.removeItem("SearchUsersFilter");

        const pageSizeNum = typeof appliedFilters.pageSize === 'string'
            ? parseInt(appliedFilters.pageSize, 10)
            : Number(appliedFilters.pageSize) || 20;

        setAppliedFilters({
            ...initialFilterState,
            pageSize: String(pageSizeNum),
            resetFilters: true,
            resetPageIndex: true,
        });

        searchUsers({ filters: {} }, "list", 0, pageSizeNum, null);
        setDataCount(capUsersFilter.length);
    }, [appliedFilters.pageSize, capUsersFilter, searchUsers]);

    const columnUniqueValues = useMemo(() => {
        const uniq = (key: keyof CapUser) => {
            const vals = [...new Set((capUsersFilter || []).map(r => String(r[key] ?? '')).filter(v => v !== ''))];
            return vals.sort((a, b) => a.localeCompare(b, undefined, { sensitivity: 'base' }));
        };
        return {
            firstName: uniq('firstName'),
            lastName: uniq('lastName'),
            email: uniq('email'),
            userRole: uniq('userRole'),
        };
    }, [capUsersFilter]);

    const columns = useMemo<Array<any>>(
        () => {
            const cols: any[] = [
                {
                    label: "First Name",
                    accessorKey: "firstName",
                    accessor: "firstName",
                    sorting: true,
                    columnValues: { type: "both", data: columnUniqueValues.firstName },
                    cell: (info: { getValue: () => any }) => info.getValue(),
                },
                {
                    label: "Last Name",
                    accessorKey: "lastName",
                    accessor: "lastName",
                    sorting: true,
                    columnValues: { type: "both", data: columnUniqueValues.lastName },
                    cell: (info: { getValue: () => any }) => info.getValue(),
                },
                {
                    label: "Email",
                    accessorKey: "email",
                    accessor: "email",
                    sorting: true,
                    columnValues: { type: "both", data: columnUniqueValues.email },
                    cell: (info: { getValue: () => any }) => info.getValue(),
                }
            ];

            if (!isClientUsers) {
                cols.push({
                    label: "User Role",
                    accessorKey: "userRole",
                    accessor: "userRole",
                    sorting: true,
                    columnValues: { type: "both", data: columnUniqueValues.userRole },
                    cell: (info: { getValue: () => any }) => info.getValue() || '',
                });
            }

            if (hasPermission) {
                cols.push({
                    label: "Action",
                    accessorKey: "action",
                    accessor: "action",
                    sorting: false,
                    filter: false,
                    filtering: false,
                    Cell: ({ row: { original } }: { row: { original: CapUser } }) => {
                        return (
                            <div className='d-flex justify-content-start gap-3'>
                                <Button
                                    variant={BUTTONS.TABLEDOWNLOAD}
                                    startIcon={<Icon.PencilFill />}
                                    onClick={() => handleEditUser(original)}
                                />
                                <Button
                                    variant={BUTTONS.TABLEDOWNLOAD}
                                    startIcon={<Icon.TrashFill />}
                                    onClick={() => handleDeleteClickMemo(original)}
                                />
                            </div>
                        );
                    }
                });
            }

            return cols;
        },
        [isClientUsers, hasPermission, handleEditUser, handleDeleteClickMemo, columnUniqueValues]
    );


    return (
        <>
            <div className="row">
                <div className="col-md-8">
                    <h5 className='_600'> Manage ABS Users</h5>

                </div>
                <div className="col-md-6 mb-3 mt-2">
                    <div className="d-flex justify-content-start align-items-center gap-2">
                        <AbsSearch
                            handleClear={handleClearFilter}
                            value={val}
                            handleChange={(e: any) => onChangeSearchInput(e.target.value)}
                            handleSearch={() => {
                                onChangeSearchInput(val);
                            }}
                            placeholder="Search by First Name, Last Name, or User Role"
                            searchApplied={searchApplied !== null}
                        />
                        <a
                            href="#"
                            className='text-link text-nowrap _500'
                            onClick={(e) => {
                                e.preventDefault();
                                handleClearFilter();
                            }}
                        >
                            Clear Filter
                        </a>
                    </div>
                </div>
                <div className="col-md-6 d-flex justify-content-end">
                    <div>
                        {((!isClientUsers && hasManageAbsUsersPermission) || (isClientUsers && hasManageClientUsersPermission)) && (
                            <Button variant={BUTTONS.PRIMARY}
                                onClick={() => {
                                    setEditUser(null);
                                    setShowAddUser(true);
                                }}
                            >Add New User</Button>
                        )}
                    </div>
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
                        data={paginatedUsers}
                        pagination={true}
                        expand={false}
                        allowColumnsShowHide={false}
                        shadow={true}
                        dataCount={dataCount}
                        pageIndex={appliedFilters.pageIndex}
                        pageSize={appliedFilters.pageSize.toString()}
                        applyFilterCallback={applyFilter}
                        appliedFilters={{
                            ...appliedFilters,
                            pageSize: appliedFilters.pageSize.toString(),
                            pageIndex: appliedFilters.pageIndex.toString(),
                        }}
                        pageOptions={["20", "40", "60", "80"]}
                        allDataProvided

                    />
                )}
            </div>
            <AddUser
                show={showAddUser}
                onHide={handleAddUserHide}
                onSaved={handleAddUserSaved}
                onError={handleAddUserError}
                editUser={editUser}
                isClientUsers={isClientUsers}
                existingUsers={capUsersFilter}
            />
            <Modal show={deleteModel} size="lg" centered onHide={() => setDeleteModel(false)} animation={false} backdrop="static">
                <Modal.Header closeButton>
                    <Modal.Title>Delete User</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    <div className="row">
                        <div className="col-md-12">
                            <div className="d-flex justify-content-center align-items-center flex-column">
                                <div className="delete-icon-wrapper mb-4">
                                    <Icon.Trash size={24} />
                                </div>
                                <p className='_500 tx-14'>
                                    Are you sure you want to delete '{deletingUser ? `${deletingUser.firstName} ${deletingUser.lastName}` : ''}' User?
                                </p>
                            </div>
                        </div>
                    </div>
                </Modal.Body>
                <Modal.Footer className='gap-3'>
                    <Button variant={BUTTONS.SECONDARY} onClick={() => {
                        setDeleteModel(false);
                        setDeletingUser(null);
                    }}>
                        Cancel
                    </Button>
                    <Button variant={BUTTONS.PRIMARY} onClick={handleDeleteConfirm} disabled={loading}>
                        {loading ? 'Deleting...' : 'Delete'}
                    </Button>
                </Modal.Footer>
            </Modal>

            {showInfoPopup && (
                <InformationPopup
                    onShow={showInfoPopup}
                    onClose={() => setShowInfoPopup(false)}
                    pageName={popupPageName}
                    message={responseMsg}
                />
            )}

        </>

    );
};
export default UsersList;