import React, { useState } from 'react';
import Modal from 'react-bootstrap/Modal';
import { Button, BUTTONS } from "customer_portal-ui-shared";
import * as Icon from 'react-bootstrap-icons';
import * as projectService from '../../service/ProjectService';

export interface SyncConfirmModalProps {
    project: { id: number; name: string } | null;
    onClose: () => void;
    onSuccess?: (released: boolean) => void;
}

export const SyncConfirmModal: React.FC<SyncConfirmModalProps> = ({
    project,
    onClose,
    onSuccess
}) => {
    const [isSyncing, setIsSyncing] = useState<boolean>(false);
    const [releaseAfterSync, setReleaseAfterSync] = useState<boolean>(false);
    const [syncSuccessModal, setSyncSuccessModal] = useState<boolean>(false);
    const [syncReleased, setSyncReleased] = useState<boolean>(false);
    const [transferError, setTransferError] = useState<string | null>(null);

    if (!project && !syncSuccessModal && !transferError) {
        return null;
    }

    const handleConfirmSync = async () => {
        if (!project?.id) return;
        setIsSyncing(true);
        try {
            const response = await projectService.syncProjectOnline(project.id, releaseAfterSync);
            if (!response.data?.success) {
                throw { response };
            }
            setSyncReleased(Boolean(response.data.released));
            setSyncSuccessModal(true);
        } catch (err: any) {
            setTransferError(projectService.getTransferErrorMessage(err, "Unable to Sync/Download due to a very low internet connection. Please proceed when your internet connection is having at least 10 mbps download and upload speed."));
        } finally {
            setIsSyncing(false);
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
            {/* Sync Confirm Modal */}
            <Modal
                show={Boolean(project) && !syncSuccessModal && !transferError}
                onHide={() => !isSyncing && onClose()}
                centered
                backdrop="static"
            >
                <Modal.Header closeButton={!isSyncing}>
                    <Modal.Title>Confirm</Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    {!isSyncing ? (
                        <>
                            <h5>{project?.name} is available for syncing.</h5>
                            <div className="d-flex justify-content-start flex-column gap-2 mt-3">
                                <div className="form-check">
                                    <input
                                        className="form-check-input"
                                        type="radio"
                                        name="saveonline"
                                        id="keepdownloaded"
                                        checked={!releaseAfterSync}
                                        onChange={() => setReleaseAfterSync(false)}
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
                                        checked={releaseAfterSync}
                                        onChange={() => setReleaseAfterSync(true)}
                                    />
                                    <label className="form-check-label" htmlFor="discardchanges">
                                        Remove offline copy
                                    </label>
                                </div>
                            </div>
                        </>
                    ) : (
                        <div className="text-center py-4">
                            <div className="progress mb-3" style={{ height: '20px' }}>
                                <div
                                    className="progress-bar progress-bar-striped progress-bar-animated bg-success"
                                    role="progressbar"
                                    style={{ width: '100%' }}
                                />
                            </div>
                        </div>
                    )}
                </Modal.Body>
                {!isSyncing && (
                    <Modal.Footer className="gap-2">
                        <Button variant={BUTTONS.SECONDARY} onClick={onClose}>
                            No, Cancel
                        </Button>
                        <Button variant={BUTTONS.PRIMARY} onClick={handleConfirmSync}>
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

            {/* Sync Success Modal */}
            <Modal
                show={syncSuccessModal}
                onHide={handleCloseSuccess}
                centered
            >
                <Modal.Header closeButton>
                    <Modal.Title className="text-success">
                        <Icon.CheckCircleFill className="me-2" />
                        Sync Successful
                    </Modal.Title>
                </Modal.Header>
                <Modal.Body>
                    <p className="mb-0">
                        Offline records and survey ratings have been synchronized to the cloud database.
                        {syncReleased
                            ? " The project has been released and removed from this computer."
                            : " The project stays on this computer for further offline work."}
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
