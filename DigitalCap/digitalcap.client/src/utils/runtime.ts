import axios from "axios";

/** True when the UI is running inside the CAP desktop (Electron) app. */
export const isElectron = (): boolean =>
    typeof navigator !== "undefined" && navigator.userAgent.toLowerCase().includes("electron");

const STATUS_CACHE_MS = 15000;
let cachedStatus: { online: boolean; checkedAt: number } | null = null;

/**
 * Whether online features (Azure AD, sync) are reachable.
 * In the desktop app the local server decides, since the browser's own flag only
 * reports a network adapter, not an internet connection.
 */
export const isAppOnline = async (): Promise<boolean> => {
    if (!navigator.onLine) return false;
    if (!isElectron()) return true;

    if (cachedStatus && Date.now() - cachedStatus.checkedAt < STATUS_CACHE_MS) {
        return cachedStatus.online;
    }

    let online: boolean;
    try {
        const response = await axios.get("/api/Offline/GetOnlineStatus", { timeout: 4000 });
        online = Boolean(response.data?.isOnline);
    } catch {
        online = navigator.onLine;
    }

    cachedStatus = { online, checkedAt: Date.now() };
    return online;
};

/** True when running in the desktop app without a connection to online services. */
export const isOfflineDesktop = async (): Promise<boolean> => isElectron() && !(await isAppOnline());

export interface OfflineUser {
    id: string;
    emailId: string;
    firstName?: string;
    lastName?: string;
}

/** The surveyor recorded at their last online sign-in, or null if there is none yet. */
export const getOfflineUser = async (): Promise<OfflineUser | null> => {
    try {
        const response = await axios.get<OfflineUser>("/api/Offline/GetOfflineUser", { timeout: 4000 });
        return response.data?.emailId ? response.data : null;
    } catch {
        return null;
    }
};
