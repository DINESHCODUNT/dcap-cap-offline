import React, { useEffect } from 'react';

export const EmptyProjectView: React.FC = () => {
    useEffect(() => {
        const checkOnlineStatus = () => {
            const isOnline = navigator.onLine;
            if (isOnline) {
                window.location.href = "/app/Project";
            }
        };

        window.addEventListener('online', checkOnlineStatus);
        window.addEventListener('offline', checkOnlineStatus);
        return () => {
            window.removeEventListener('online', checkOnlineStatus);
            window.removeEventListener('offline', checkOnlineStatus);
        };
    }, []);

    return (
        <div style={{
            fontFamily: "'Arial', sans-serif",
            backgroundColor: "#f4f4f4",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            height: "100vh",
            margin: 0
        }}>
            <div style={{
                backgroundColor: "#fff",
                padding: "20px",
                borderRadius: "8px",
                boxShadow: "0 0 10px rgba(0, 0, 0, 0.1)"
            }}>
                <h2 style={{ textAlign: "center" }}>There are no projects that have been downloaded yet.</h2>
                <h4>It is recommended that you download at least one project when in online mode.</h4>
            </div>
        </div>
    );
};

export default EmptyProjectView;
