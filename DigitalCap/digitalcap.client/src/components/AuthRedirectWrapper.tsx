import React, { useEffect, useState } from "react";
import { InteractionStatus } from "@azure/msal-browser";
import { useMsal } from "@azure/msal-react";
import { getLoginScopes } from "../utils/authConfig";
import { Spinner } from "react-bootstrap";

const DOMAIN_HINT = import.meta.env.VITE_APP_DOMAIN_HINT;

export const AuthRedirectWrapper: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const { instance, inProgress } = useMsal();
    const [msalInitialized, setMsalInitialized] = useState(false);

    useEffect(() => {
        // Clean up any stale postLoginRedirect stored in localStorage
        localStorage.removeItem("postLoginRedirect");
    }, []);

    useEffect(() => {
        const handleRedirect = async () => {
            try {
                const redirectResult = await instance.handleRedirectPromise();
                if (!redirectResult) {
                    const accounts = instance.getAllAccounts();
                    const isInternalRoute = window.location.pathname.toLowerCase().includes("/internal");

                    if (accounts.length === 0 && isInternalRoute) {
                        if (inProgress === InteractionStatus.None) {
                            await instance.loginRedirect({
                                scopes: getLoginScopes(),
                                domainHint: DOMAIN_HINT
                            });
                        }
                        return;
                    }
                }
                setMsalInitialized(true);
            } catch (error: any) {
            }
        };

        handleRedirect();
    }, [instance, inProgress]);

    if (!msalInitialized) {
        return (
            <div className="d-flex flex-column justify-content-center align-items-center min-vh-100">
                <Spinner animation="border" role="status" variant="primary">
                    <span className="visually-hidden">Loading...</span>
                </Spinner>
                <div className="mt-3 text-muted">Loading Auth...</div>
            </div>
        );
    }

    return <>{children}</>;
};
