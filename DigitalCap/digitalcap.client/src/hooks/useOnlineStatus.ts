import { useState, useEffect, useCallback } from 'react';
import axios from 'axios';

export interface OnlineStatus {
    isOnline: boolean;
    mode: 'Online' | 'Offline';
    lastChecked: Date;
    checkStatus: () => Promise<boolean>;
}

export const useOnlineStatus = (): OnlineStatus => {
    const [isOnline, setIsOnline] = useState<boolean>(navigator.onLine);
    const [lastChecked, setLastChecked] = useState<Date>(new Date());

    const checkStatus = useCallback(async (): Promise<boolean> => {
        try {
            const response = await axios.get('/api/Offline/GetOnlineStatus', { timeout: 4000 });
            const online = Boolean(response.data?.isOnline);
            setIsOnline(online);
            setLastChecked(new Date());
            return online;
        } catch {
            const browserOnline = navigator.onLine;
            setIsOnline(browserOnline);
            setLastChecked(new Date());
            return browserOnline;
        }
    }, []);

    useEffect(() => {
        const handleOnline = () => {
            checkStatus();
        };

        const handleOffline = () => {
            setIsOnline(false);
            setLastChecked(new Date());
        };

        window.addEventListener('online', handleOnline);
        window.addEventListener('offline', handleOffline);

        // Initial check and periodic polling every 30 seconds
        checkStatus();
        const intervalId = setInterval(checkStatus, 30000);

        return () => {
            window.removeEventListener('online', handleOnline);
            window.removeEventListener('offline', handleOffline);
            clearInterval(intervalId);
        };
    }, [checkStatus]);

    return {
        isOnline,
        mode: isOnline ? 'Online' : 'Offline',
        lastChecked,
        checkStatus
    };
};
