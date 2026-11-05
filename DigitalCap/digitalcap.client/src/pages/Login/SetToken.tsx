import React, { useEffect } from "react";
import { useNavigate } from "react-router-dom";
import AppLoader from "../../Common/AppLoader";

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

    return <AppLoader />;
};

export default SetToken;
