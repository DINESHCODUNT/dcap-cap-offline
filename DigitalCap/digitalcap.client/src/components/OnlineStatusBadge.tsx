import React from 'react';
import { useOnlineStatus } from '../hooks/useOnlineStatus';
import * as Icon from 'react-bootstrap-icons';

interface OnlineStatusBadgeProps {
    className?: string;
    showLabel?: boolean;
}

export const OnlineStatusBadge: React.FC<OnlineStatusBadgeProps> = ({
    className = '',
    showLabel = true
}) => {
    const { isOnline } = useOnlineStatus();

    return (
        <div
            className={`d-inline-flex align-items-center px-2 py-1 rounded-pill ${className}`}
            style={{
                backgroundColor: isOnline ? 'rgba(40, 167, 69, 0.15)' : 'rgba(255, 193, 7, 0.2)',
                color: isOnline ? '#28a745' : '#ffc107',
                border: `1px solid ${isOnline ? 'rgba(40, 167, 69, 0.3)' : 'rgba(255, 193, 7, 0.4)'}`,
                fontSize: '0.82rem',
                fontWeight: 600,
                letterSpacing: '0.3px',
                userSelect: 'none'
            }}
            title={isOnline ? 'Connected to Azure Network' : 'Working in Local Offline Mode'}
        >
            <span
                style={{
                    display: 'inline-block',
                    width: '8px',
                    height: '8px',
                    borderRadius: '50%',
                    backgroundColor: isOnline ? '#28a745' : '#ffc107',
                    marginRight: showLabel ? '6px' : '0',
                    boxShadow: isOnline ? '0 0 6px #28a745' : '0 0 6px #ffc107'
                }}
            />
            {showLabel && (
                <span>
                    {isOnline ? (
                        <>
                            <Icon.CloudCheckFill className="me-1" style={{ verticalAlign: '-1px' }} />
                            Online
                        </>
                    ) : (
                        <>
                            <Icon.CloudSlashFill className="me-1" style={{ verticalAlign: '-1px' }} />
                            Offline Mode
                        </>
                    )}
                </span>
            )}
        </div>
    );
};

export default OnlineStatusBadge;
