import React from "react";
import { LoadingIcon } from "customer_portal-ui-shared";

export const DEFAULT_LOADING_MESSAGE = "Loading Data . . .";

interface AppLoaderProps {
    message?: string;
}

const AppLoader: React.FC<AppLoaderProps> = ({ message = DEFAULT_LOADING_MESSAGE }) => (
    <div className="app-loader" role="status" aria-live="polite">
        <LoadingIcon loaderMessage={message} />
    </div>
);

export default AppLoader;
