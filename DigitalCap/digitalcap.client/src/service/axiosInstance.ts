import axios from "axios";
import type { AxiosInstance, AxiosResponse, AxiosError } from "axios";
import initializeMsal from "../msalInstance";
import { API_BASE_URL } from "../api/apiConfig";
import { isOfflineDesktop } from "../utils/runtime";
import type { SilentRequest, PopupRequest } from "@azure/msal-browser";

const axiosInstance: AxiosInstance = axios.create({
    baseURL: API_BASE_URL,
    headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
    },
});

const API_SCOPES = ["d8a3a596-6fcc-4047-ac76-d990b30f1bb0/.default"];

const getRedirectUri = () => `${window.location.origin}/success`;

const renewToken = async (): Promise<string | null> => {
    try {
        const msalInstance = await initializeMsal();
        const accounts = msalInstance.getAllAccounts();

        if (!accounts.length) {
            return null;
        }

        const silentRequest: SilentRequest = {
            account: accounts[0],
            scopes: API_SCOPES,
            redirectUri: getRedirectUri(),
        };

        try {
            const response = await msalInstance.acquireTokenSilent(silentRequest);
            const token = response.accessToken;
            if (token) {
                localStorage.setItem("token", token);
                return token;
            }
        } catch {
            const popupRequest: PopupRequest = {
                account: accounts[0],
                scopes: API_SCOPES,
                redirectUri: getRedirectUri(),
            };
            const response = await msalInstance.acquireTokenPopup(popupRequest);
            const token = response.accessToken;
            if (token) {
                localStorage.setItem("token", token);
                return token;
            }
        }

        return null;
    } catch {
        return null;
    }
};

axiosInstance.interceptors.request.use(async (config) => {
    // Offline in the desktop app the local server identifies the user itself,
    // and token renewal would only fail (or open a sign-in popup) without a network.
    if (await isOfflineDesktop()) {
        return config;
    }

    let token = localStorage.getItem("token");

    if (!token) {
        token = await renewToken();
    }

    if (token && config.headers) {
        config.headers.Authorization = `Bearer ${token}`;
    }

    return config;
});

let isRefreshing = false;
let pendingRequests: Array<(token: string | null) => void> = [];

axiosInstance.interceptors.response.use(
    (response: AxiosResponse) => response,
    async (error: AxiosError) => {
        if (error.response?.status !== 401) {
            return Promise.reject(error);
        }

        const originalConfig = error.config as any;

        if (await isOfflineDesktop()) {
            return Promise.reject(error);
        }

        if (originalConfig._retried) {
            localStorage.removeItem("token");
            return Promise.reject(error);
        }

        originalConfig._retried = true;
        localStorage.removeItem("token");

        if (isRefreshing) {
            return new Promise((resolve, reject) => {
                pendingRequests.push((token) => {
                    if (token) {
                        originalConfig.headers!.Authorization = `Bearer ${token}`;
                        resolve(axiosInstance(originalConfig));
                    } else {
                        reject(error);
                    }
                });
            });
        }

        isRefreshing = true;
        const newToken = await renewToken();
        isRefreshing = false;

        pendingRequests.forEach((cb) => cb(newToken));
        pendingRequests = [];

        if (newToken) {
            originalConfig.headers!.Authorization = `Bearer ${newToken}`;
            return axiosInstance(originalConfig);
        }

        return Promise.reject(error);
    }
);

export default axiosInstance;
