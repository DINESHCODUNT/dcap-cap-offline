import React, { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { Spinner } from "react-bootstrap";

const SetToken: React.FC = () => {
    const navigate = useNavigate();

    useEffect(() => {
        const hash = window.location.hash.startsWith("#")
            ? window.location.hash.substring(1)
            : window.location.hash;
        const params = new URLSearchParams(hash);
        const token = params.get("token");
        const redirectURL = params.get("redirectURL");

        if (token) {
            localStorage.setItem("token", token);
        }

        window.history.replaceState(null, "", "/SetToken");

        const target =
            redirectURL && redirectURL.startsWith("/") && !redirectURL.startsWith("//")
                ? redirectURL
                : "/app/Project";

        navigate(target, { replace: true });
    }, [navigate]);

    return (
        <div className="d-flex justify-content-center align-items-center min-vh-100">
            <Spinner animation="border" role="status" variant="primary">
                <span className="visually-hidden">Loading...</span>
            </Spinner>
        </div>
    );
};

export default SetToken;
