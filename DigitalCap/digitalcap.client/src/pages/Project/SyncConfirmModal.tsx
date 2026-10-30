import React, { useState, useEffect, useRef } from 'react';
import axios from 'axios';
import Modal from 'react-bootstrap/Modal';
import { Button, BUTTONS } from "customer_portal-ui-shared";
import * as Icon from 'react-bootstrap-icons';
import * as projectService from '../../service/ProjectService';
import { usePopup } from '../../Common/PopupProvider';
import { measureConnectionSpeed, isNetworkConnectionError } from '../../utils/connectionSpeed';
import TransferProgressBar from '../../Common/TransferProgressBar';

export interface SyncConfirmModalProps {
    project: { id: number; name: string } | null;
    onClose: () => void;
    onSuccess?: (released: boolean) => void;
}

type SyncOption = 'keep' | 'remove' | 'discardWithoutSync';
type CancelledOption = 'redownload' | 'discard';

export const SyncConfirmModal: React.FC<SyncConfirmModalProps> = ({
    project,
    onClose,
    onSuccess
}) => {
    const popup = usePopup();
    const [isCheckingLock, setIsCheckingLock] = useState<boolean>(false);
    const [isCancelled, setIsCancelled] = useState<boolean>(false);
    const [syncOption, setSyncOption] = useState<SyncOption>('keep');
    const [cancelledOption, setCancelledOption] = useState<CancelledOption>('redownload');
    const [showDiscardConfirm, setShowDiscardConfirm] = useState<boolean>(false);

    const [isSyncing, setIsSyncing] = useState<boolean>(false);
    const [isDownloading, setIsDownloading] = useState<boolean>(false);
    const [isReleasing, setIsReleasing] = useState<boolean>(false);

    const [isSyncInBackground, setIsSyncInBackground] = useState<boolean>(false);
    const [syncAbortController, setSyncAbortController] = useState<AbortController | null>(null);

    const [syncSuccessModal, setSyncSuccessModal] = useState<boolean>(false);
    const [syncReleased, setSyncReleased] = useState<boolean>(false);
    const [transferError, setTransferError] = useState<string | null>(null);
    const [progressPercent, setProgressPercent] = useState<number>(0);
    const progressPollRef = useRef<ReturnType<typeof setInterval> | null>(null);

    useEffect(() => {
        return () => {
            if (progressPollRef.current) {
                clearInterval(progressPollRef.current);
                progressPollRef.current = null;
            }
        };
    }, []);

    const isBusy = isSyncing || isDownloading || isReleasing;

    useEffect(() => {
        if (!project?.id) {
            setIsCheckingLock(false);
            setIsCancelled(false);
            setSyncOption('keep');
            setCancelledOption('redownload');
            setShowDiscardConfirm(false);
            return;
        }

        let isMounted = true;
        setIsCheckingLock(true);
        setIsCancelled(false);
        setSyncOption('keep');
        setCancelledOption('redownload');
        setShowDiscardConfirm(false);

        measureConnectionSpeed()
            .then(async (mbps) => {
                if (!isMounted) return;
                if (mbps === null || mbps < 10) {
                    setIsCheckingLock(false);
                    onClose();
                    await popup.info(
                        "Unable to Sync/Download due to a very low internet connection. Please proceed when your internet connection is having at least 10 mbps download and upload speed.",
                        "Confirm"
                    );
                    return;
                }

                projectService.getDownloadedDataExists(project.id)
                    .then((res) => {
                        if (!isMounted) return;
                        // When the online lock is gone, the endpoint returns null / false
                        const lockExists = Boolean(res.data);
                        setIsCancelled(!lockExists);
                    })
                    .catch((err) => {
                        if (!isMounted) return;
                        if (isNetworkConnectionError(err)) {
                            onClose();
                            popup.info({
                                title: "Connection Error",
                                message: "The sync process has been interrupted due to a loss of network connection. Please check your internet connection and try again.",
                                okText: "Close"
                            });
                            return;
                        }
                        setTransferError(projectService.getTransferErrorMessage(err, "Unable to check online project status."));
                    })
                    .finally(() => {
                        if (isMounted) {
                            setIsCheckingLock(false);
                        }
                    });
            })
            .catch(async () => {
                if (!isMounted) return;
                setIsCheckingLock(false);
                onClose();
                await popup.info(
                    "Unable to Sync/Download due to a very low internet connection. Please proceed when your internet connection is having at least 10 mbps download and upload speed.",
                    "Confirm"
                );
            });

        return () => {
            isMounted = false;
        };
    }, [project?.id]);

    if (!project && !syncSuccessModal && !transferError && !showDiscardConfirm) {
        return null;
    }

    const handleConfirmSync = async () => {
        if (!project?.id) return;

        if (syncOption === 'discardWithoutSync') {
            setShowDiscardConfirm(true);
            return;
        }

        setProgressPercent(0);
        if (progressPollRef.current) {
            clearInterval(progressPollRef.current);
            progressPollRef.current = null;
        }

        const release = syncOption === 'remove';
        setIsSyncing(true);
        setIsSyncInBackground(false);
        const controller = new AbortController();
        setSyncAbortController(controller);

        progressPollRef.current = setInterval(async () => {
            try {
                const res = await projectService.getTransferProgress(project.id);
                if (typeof res?.data?.percent === 'number') {
                    setProgressPercent(res.data.percent);
                }
            } catch {
                // ignore polling errors
            }
        }, 500);

        try {
            const response = await projectService.syncProjectOnline(project.id, release, controller.signal);
            if (progressPollRef.current) {
                clearInterval(progressPollRef.current);
                progressPollRef.current = null;
            }
            if (!response.data?.success) {
                setProgressPercent(0);
                throw { response };
            }
            setProgressPercent(100);
            setSyncReleased(Boolean(response.data.released));
            setSyncSuccessModal(true);
        } catch (err: any) {
            if (progressPollRef.current) {
                clearInterval(progressPollRef.current);
                progressPollRef.current = null;
            }
            setProgressPercent(0);
            if (axios.isCancel(err) || err?.name === 'CanceledError' || err?.name === 'AbortError') {
                return;
            }
            if (isNetworkConnectionError(err)) {
                setIsSyncing(false);
                setIsSyncInBackground(false);
                setSyncAbortController(null);
                onClose();
                await popup.info({
                    title: "Connection Error",
                    message: "The sync process has been interrupted due to a loss of network connection. Please check your internet connection and try again.",
                    okText: "Close"
                });
                return;
            }
            setTransferError(projectService.getTransferErrorMessage(err, "Unable to Sync/Download due to a very low internet connection. Please proceed when your internet connection is having at least 10 mbps download and upload speed."));
        } finally {
            if (progressPollRef.current) {
                clearInterval(progressPollRef.current);
                progressPollRef.current = null;
            }
            setIsSyncing(false);
            setSyncAbortController(null);
        }
    };

    const handleStopSync = () => {
        if (progressPollRef.current) {
            clearInterval(progressPollRef.current);
            progressPollRef.current = null;
        }
        setProgressPercent(0);
        if (syncAbortController) {
            syncAbortController.abort();
        }
        setIsSyncing(false);
        setIsSyncInBackground(false);
        setSyncAbortController(null);
        popup.error("Sync Failed", "Error");
        onClose();
    };

    const handleConfirmCancelledAction = async () => {
        if (!project?.id) return;

        if (cancelledOption === 'redownload') {
            setProgressPercent(0);
            if (progressPollRef.current) {
                clearInterval(progressPollRef.current);
                progressPollRef.current = null;
            }
            setIsDownloading(true);

            progressPollRef.current = setInterval(async () => {
                try {
                    const res = await projectService.getTransferProgress(project.id);
                    if (typeof res?.data?.percent === 'number') {
                        setProgressPercent(res.data.percent);
                    }
                } catch {
                    // ignore polling errors
                }
            }, 500);

            try {
                const response = await projectService.downloadProjectOffline(project.id, undefined, true);
                if (progressPollRef.current) {
                    clearInterval(progressPollRef.current);
                    progressPollRef.current = null;
                }
                if (response.data && response.data.success === false) {
                    setProgressPercent(0);
                    throw { response };
                }
                setProgressPercent(100);
                popup.success("Download completed successfully", "Success");
                onSuccess?.(false);
                onClose();
            } catch (err: any) {
                if (progressPollRef.current) {
                    clearInterval(progressPollRef.current);
                    progressPollRef.current = null;
                }
                setProgressPercent(0);
                if (isNetworkConnectionError(err)) {
                    setIsDownloading(false);
                    onClose();
                    await popup.info({
                        title: "Connection Error",
                        message: "The download/upload process has been interrupted due to a loss of network connection. Please check your internet connection and try again.",
                        okText: "Close"
                    });
                    return;
                }
                setTransferError(projectService.getTransferErrorMessage(err, "Unable to download project."));
            } finally {
                if (progressPollRef.current) {
                    clearInterval(progressPollRef.current);
                    progressPollRef.current = null;
                }
                setIsDownloading(false);
            }
        } else {
            setIsReleasing(true);
            try {
                const response = await projectService.releaseProjectWithoutSync(project.id);
                if (response.data && response.data.success === false) {
                    throw { response };
                }
                onSuccess?.(true);
                onClose();
            } catch (err: any) {
                setTransferError(projectService.getTransferErrorMessage(err, "Unable to release project."));
            } finally {
                setIsReleasing(false);
            }
        }
    };

    const handleConfirmDiscardWithoutSync = async () => {
        if (!project?.id) return;
        setIsReleasing(true);
        setShowDiscardConfirm(false);
        try {
            const response = await projectService.releaseProjectWithoutSync(project.id);
            if (response.data && response.data.success === false) {
                throw { response };
            }
            onSuccess?.(true);
            onClose();
        } catch (err: any) {
            setTransferError(projectService.getTransferErrorMessage(err, "Unable to release project."));
        } finally {
            setIsReleasing(false);
        }
    };

    const handleCloseSuccess = () => {
        setSyncSuccessModal(false);
        onSuccess?.(syncReleased);
        onClose();
    };

    const handleCloseError = () => {
        setTransferError(null);
        onClose();
    };

    return (
        <>
            {/* Sync / Cancelled Modal */}
            <Modal
                show={Boolean(project) && !syncSuccessModal && !transferError && !showDiscardConfirm && !isSyncing && !isCheckingLock}
                onHide={() => !isBusy && onClose()}
                centered
                backdrop="static"
            >
                <Modal.Header closeButton={!isBusy}>
                    <Modal.Title>Confirm</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    {isDownloading ? (
                        <>
                            <div className="d-flex align-items-center gap-3 mb-2">
                                <Icon.Download className="text-primary" size={24} />
                                <h5 className="mb-0">Downloading the {project?.name} project in Offline Mode</h5>
                            </div>
                            <div className="text-center py-4">
                                <div className="mb-3">
                                    <TransferProgressBar percent={progressPercent} height="20px" variant="bg-success" />
                                </div>
                            </div>
                        </>
                    ) : isBusy ? (
                        <div className="text-center py-4">
                            <div className="mb-3">
                                <TransferProgressBar percent={progressPercent} height="20px" variant="bg-success" />
                            </div>
                        </div>
                    ) : isCancelled ? (
                        <>
                            <p>
                                CAP Administrator has cancelled the project from offline download.  You may contact CAP HQ for further queries.
                            </p>
                            <div className="d-flex justify-content-start flex-column gap-2 mt-3">
                                <div className="form-check">
                                    <input
                                        className="form-check-input"
                                        type="radio"
                                        name="saveonlines"
                                        id="downloadedagain"
                                        checked={cancelledOption === 'redownload'}
                                        onChange={() => setCancelledOption('redownload')}
                                    />
                                    <label className="form-check-label" htmlFor="downloadedagain">
                                        Re-Download
                                    </label>
                                </div>
                                <div className="form-check">
                                    <input
                                        className="form-check-input"
                                        type="radio"
                                        name="saveonlines"
                                        id="discardchanged"
                                        checked={cancelledOption === 'discard'}
                                        onChange={() => setCancelledOption('discard')}
                                    />
                                    <label className="form-check-label" htmlFor="discardchanged">
                                        Discard Local Copy
                                    </label>
                                </div>
                            </div>
                            {cancelledOption === 'redownload' ? (
                                <div className="alert alert-primary mt-3">
                                    <p className="mb-0 pb-0">
                                        Projects's offline data will be lost and updated with the online information
                                    </p>
                                </div>
                            ) : (
                                <div className="alert alert-primary mt-3">
                                    <p className="mb-0 pb-0">
                                        By Discarding this local copy may result in lost data, if the user has not synced the latest changes
                                    </p>
                                </div>
                            )}
                        </>
                    ) : (
                        <>
                            <h5>{project?.name} is available for syncing.</h5>
                            <div className="d-flex justify-content-start flex-column gap-2 mt-3">
                                <div className="form-check">
                                    <input
                                        className="form-check-input"
                                        type="radio"
                                        name="saveonline"
                                        id="keepdownloaded"
                                        checked={syncOption === 'keep'}
                                        onChange={() => setSyncOption('keep')}
                                    />
                                    <label className="form-check-label" htmlFor="keepdownloaded">
                                        Keep project offline
                                    </label>
                                </div>
                                <div className="form-check">
                                    <input
                                        className="form-check-input"
                                        type="radio"
                                        name="saveonline"
                                        id="discardchanges"
                                        checked={syncOption === 'remove'}
                                        onChange={() => setSyncOption('remove')}
                                    />
                                    <label className="form-check-label" htmlFor="discardchanges">
                                        Remove offline copy
                                    </label>
                                </div>
                                <div className="form-check">
                                    <input
                                        className="form-check-input"
                                        type="radio"
                                        name="saveonline"
                                        id="discardchangeswithoutsync"
                                        checked={syncOption === 'discardWithoutSync'}
                                        onChange={() => setSyncOption('discardWithoutSync')}
                                    />
                                    <label className="form-check-label" htmlFor="discardchangeswithoutsync">
                                        Do not sync the offline data
                                    </label>
                                </div>
                            </div>
                            {syncOption === 'keep' && (
                                <div className="alert alert-primary mt-3">
                                    <p className="mb-0 pb-0">
                                        Data will be saved online and the project will remain offline.
                                    </p>
                                </div>
                            )}
                            {syncOption === 'remove' && (
                                <div className="alert alert-primary mt-3">
                                    <p className="mb-0 pb-0">
                                        Data will be saved online and the project will be removed from offline.
                                    </p>
                                </div>
                            )}
                            {syncOption === 'discardWithoutSync' && (
                                <div className="alert alert-primary mt-3">
                                    <p className="mb-0 pb-0">
                                        If you proceed, any data entered in the offline report will be discarded.
                                    </p>
                                </div>
                            )}
                        </>
                    )}
                </Modal.Body>
                {!isBusy && (
                    <Modal.Footer className="gap-2">
                        <Button variant={BUTTONS.SECONDARY} onClick={onClose}>
                            No, Cancel
                        </Button>
                        <Button
                            variant={BUTTONS.PRIMARY}
                            onClick={isCancelled ? handleConfirmCancelledAction : handleConfirmSync}
                        >
                            Yes, Proceed
                        </Button>
                    </Modal.Footer>
                )}
            </Modal>

            {/* Discard Without Sync ("Are you sure?") Confirm Modal */}
            <Modal
                show={showDiscardConfirm && !transferError}
                onHide={() => !isReleasing && setShowDiscardConfirm(false)}
                centered
                backdrop="static"
            >
                <Modal.Header closeButton={!isReleasing}>
                    <Modal.Title>Confirm</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    {isReleasing ? (
                        <div className="text-center py-4">
                            <div className="progress mb-3" style={{ height: '20px' }}>
                                <div
                                    className="progress-bar progress-bar-striped progress-bar-animated bg-success"
                                    role="progressbar"
                                    style={{ width: '100%' }}
                                />
                            </div>
                        </div>
                    ) : (
                        <>
                            <h5>Are you sure?</h5>
                            <p className="mb-0 pb-0">
                                If you proceed, any data entered in the offline report will be discarded.
                            </p>
                        </>
                    )}
                </Modal.Body>
                {!isReleasing && (
                    <Modal.Footer className="gap-2">
                        <Button variant={BUTTONS.SECONDARY} onClick={() => setShowDiscardConfirm(false)}>
                            No, Cancel
                        </Button>
                        <Button variant={BUTTONS.PRIMARY} onClick={handleConfirmDiscardWithoutSync}>
                            Yes, Proceed
                        </Button>
                    </Modal.Footer>
                )}
            </Modal>

            {/* Error Modal */}
            <Modal
                show={Boolean(transferError)}
                onHide={handleCloseError}
                centered
            >
                <Modal.Header closeButton>
                    <Modal.Title>Confirm</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    <p className="mb-0">
                        {transferError || "Unable to Sync/Download due to a very low internet connection. Please proceed when your internet connection is having at least 10 mbps download and upload speed."}
                    </p>
                </Modal.Body>
                <Modal.Footer>
                    <Button variant={BUTTONS.SECONDARY} onClick={handleCloseError}>
                        Ok
                    </Button>
                </Modal.Footer>
            </Modal>

            {/* Sync In Progress Modal */}
            <Modal
                show={isSyncing && !isSyncInBackground}
                onHide={handleStopSync}
                centered
                backdrop="static"
            >
                <Modal.Header closeButton>
                </Modal.Header>
                <Modal.Body>
                    <div className="d-flex align-items-center gap-3 mb-3">
                        <div className="img-wrapper">
                            <Icon.ArrowRepeat className="text-primary" size={24} />
                        </div>
                        <div className="alert-content flex-grow-1">
                            <h5 className="mb-0">Synchronizing Offline data to Online mode</h5>
                        </div>
                    </div>
                    <TransferProgressBar percent={progressPercent} height="10px" variant="bg-primary" />
                </Modal.Body>
                <Modal.Footer className="d-flex justify-content-end gap-2">
                    <Button variant={BUTTONS.SECONDARY} onClick={handleStopSync}>
                        No, Cancel
                    </Button>
                    <Button variant={BUTTONS.SECONDARY} onClick={() => setIsSyncInBackground(true)}>
                        Yes, Sync in Background
                    </Button>
                </Modal.Footer>
            </Modal>

            {isSyncInBackground && isSyncing && (
                <div className="minimize-section" style={{ position: 'fixed', bottom: '20px', right: '20px', zIndex: 1050 }}>
                    <div className="minimize-card card shadow" style={{ minWidth: '320px', backgroundColor: '#fff', border: '1px solid #dee2e6', borderRadius: '8px' }}>
                        <div className="card-body p-3">
                            <div className="d-flex align-items-center gap-3">
                                <div className="img-wrapper">
                                    <Icon.ArrowRepeat className="text-primary" size={20} />
                                </div>
                                <div className="alert-content flex-grow-1">
                                    <h6 className="mb-1" style={{ fontSize: '14px', fontWeight: 600 }}>Just give us a moment to sync the project</h6>
                                    <TransferProgressBar percent={progressPercent} height="6px" variant="bg-primary" />
                                </div>
                            </div>
                            <div className="actions d-flex justify-content-end gap-2 mt-2" style={{ gap: '8px' }}>
                                <button type="button" className="btn btn-sm btn-light" onClick={handleStopSync}> No, Cancel </button>
                                <button type="button" className="btn btn-sm btn-light" onClick={() => setIsSyncInBackground(false)}>
                                    <Icon.ArrowsAngleExpand />
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* Sync Success Modal */}
            <Modal
                show={syncSuccessModal}
                onHide={handleCloseSuccess}
                centered
            >
                <Modal.Header closeButton>
                    <Modal.Title>
                        Success
                    </Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    <p className="mb-0">
                        Syncing of the {project?.name} Offline Project with the Online Application has been successfully completed
                    </p>
                </Modal.Body>
                <Modal.Footer>
                    <Button variant={BUTTONS.PRIMARY} onClick={handleCloseSuccess}>
                        Ok
                    </Button>
                </Modal.Footer>
            </Modal>
        </>
    );
};

export default SyncConfirmModal;
