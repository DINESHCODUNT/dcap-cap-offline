import React from 'react';
import * as Icon from 'react-bootstrap-icons';
import { Button, BUTTONS } from 'customer_portal-ui-shared';
import { useOnlineStatus } from '../../hooks/useOnlineStatus';

export interface EmptyProjectViewProps {
    onRetry?: () => void;
}

export const EmptyProjectView: React.FC<EmptyProjectViewProps> = ({ onRetry }) => {
    const { isOnline, checkStatus } = useOnlineStatus();

    const handleCheckAgain = async () => {
        await checkStatus();
        if (onRetry) {
            onRetry();
        }
    };

    return (
        <div
            className="d-flex align-items-center justify-content-center"
            style={{
                minHeight: '65vh',
                padding: '2rem'
            }}
        >
            <div
                className="card shadow-sm border-0 text-center p-4 p-md-5"
                style={{
                    maxWidth: '560px',
                    borderRadius: '12px',
                    backgroundColor: '#ffffff'
                }}
            >
                <div className="mb-4">
                    <div
                        className="d-inline-flex align-items-center justify-content-center rounded-circle"
                        style={{
                            width: '80px',
                            height: '80px',
                            backgroundColor: '#eef2f7',
                            color: '#002f6c'
                        }}
                    >
                        <Icon.CloudDownload size={42} />
                    </div>
                </div>

                <h3 className="mb-3" style={{ color: '#1a202c', fontWeight: 600 }}>
                    There are no projects that have been downloaded yet.
                </h3>

                <p className="text-muted mb-4" style={{ fontSize: '1.05rem', lineHeight: '1.6' }}>
                    It is recommended that you download at least one project when in online mode to work offline.
                </p>

                {isOnline ? (
                    <div className="alert alert-success d-flex align-items-center justify-content-center gap-2 mb-4">
                        <Icon.CheckCircleFill />
                        <span>You are currently online. You can download projects from the list.</span>
                    </div>
                ) : (
                    <div className="alert alert-warning d-flex align-items-center justify-content-center gap-2 mb-4">
                        <Icon.ExclamationTriangleFill />
                        <span>You are currently offline. Connect to the internet to download projects.</span>
                    </div>
                )}

                <div className="d-flex justify-content-center gap-3">
                    <Button
                        variant={BUTTONS.PRIMARY}
                        startIcon={<Icon.ArrowRepeat />}
                        onClick={handleCheckAgain}
                    >
                        Check Online Status
                    </Button>
                </div>
            </div>
        </div>
    );
};

export default EmptyProjectView;
