import { type JSX } from "react";
import { Navigate } from "react-router-dom";
import { Spinner } from "react-bootstrap";
import { usePermissions } from "../context/PermissionContext";

interface Props {
    allowedRoles?: string[];
    allowedPermissions?: string[];
    allowedPermissionAny?: string[];
    requireAnyAccess?: boolean;
    children: JSX.Element;
}

const RoleProtectedRoute = ({
    allowedRoles,
    allowedPermissions,
    allowedPermissionAny,
    requireAnyAccess,
    children
}: Props) => {
    const { loadingPermissions, hasRole, hasGlobalPermission, permissions, roles, pagePermissions } = usePermissions();

    if (loadingPermissions) {
        return (
            <div className="d-flex flex-column justify-content-center align-items-center min-vh-100">
                <Spinner animation="border" role="status" variant="primary">
                    <span className="visually-hidden">Loading permissions...</span>
                </Spinner>
                <div className="mt-3 text-muted">Loading permissions...</div>
            </div>
        );
    }

    let isAllowed = true;

    const isDesktop = typeof window !== 'undefined' && (Boolean((window as any).electron) || navigator.userAgent.includes('Electron'));

    if (requireAnyAccess && !isDesktop) {
        if (permissions.length === 0 && roles.length === 0 && Object.keys(pagePermissions).length === 0) {
            isAllowed = false;
        }
    }

    if (allowedRoles && allowedRoles.length > 0) {
        isAllowed = isAllowed && allowedRoles.some(role => hasRole(role));
    }

    if (allowedPermissions && allowedPermissions.length > 0) {
        isAllowed = isAllowed && allowedPermissions.every(perm => hasGlobalPermission(perm));
    }

    if (allowedPermissionAny && allowedPermissionAny.length > 0) {
        isAllowed = isAllowed && allowedPermissionAny.some(perm => hasGlobalPermission(perm));
    }

    if (!isAllowed) {
        return <Navigate to="/app/access-denied" replace />;
    }

    return children;
};

export default RoleProtectedRoute;
