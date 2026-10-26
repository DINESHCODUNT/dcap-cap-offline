import Modal from 'react-bootstrap/Modal';
import { Button } from 'customer_portal-ui-shared';
import { GetTitle, GetPopupMessage, Buttons } from './UtilityMessage';
import Spinner from 'react-bootstrap/Spinner';

interface InformationPopupProps {
    onShow?: boolean;
    show?: boolean;
    onClose: () => void;
    pageName?: string;
    message?: string;
    name?: string;
    status?: string;
    applicationName?: string;
    skippedExaminationNames?: string;
    loading?: boolean;
    onDownload?: () => void;
    Continue?: () => void;
    okClose?: () => void;
    nextPage?: boolean;
}

export default function InformationPopup({
    onShow,
    show,
    onClose,
    pageName,
    message,
    status,
    loading,
    onDownload,
    Continue,
    okClose,
    nextPage
}: InformationPopupProps) {

    const handleClose = () => {
        onClose();
    };
    return (
        <>
            <Modal
                size="lg"
                show={onShow ?? show}
                onHide={handleClose}
                centered
                className="information-popup-modal"
                backdropClassName="information-popup-backdrop"
            >
                <Modal.Header>
                    <Modal.Title>
                        {GetTitle(pageName) || "Manage Grading"}
                    </Modal.Title>
                    <button
                        type="button"
                        className="btn-close"
                        data-bs-dismiss="modal"
                        onClick={handleClose}
                        aria-label="Close"
                    ></button>
                </Modal.Header>
                <Modal.Body>
                    <p className="px-10">
                        {message || GetPopupMessage(pageName)}
                    </p>
                    <div className="d-flex justify-content-end fixed-action-btns gap-2">
                        {pageName === 'FileSizeExceeded' || pageName === 'FileType' ? (
                            <>
                                {loading ? (
                                    <Button variant="primary" disabled>
                                        <Spinner
                                            size="sm"
                                            animation="border"
                                        />
                                    </Button>
                                ) : (
                                    onDownload && (
                                        <Button onClick={onDownload} variant="primary">
                                            Download File
                                        </Button>
                                    )
                                )}
                                <Button onClick={handleClose} variant="secondary">
                                    Cancel
                                </Button>
                            </>
                        ) : (
                            <>
                                {nextPage && Continue && (
                                    <Button onClick={Continue}>{Buttons.Continue}</Button>
                                )}
                                {status ? (
                                    <Button onClick={okClose}>{Buttons.ok}</Button>
                                ) : (
                                    <Button onClick={handleClose}>{Buttons.ok}</Button>
                                )}
                            </>
                        )}
                    </div>
                </Modal.Body>
            </Modal>
        </>
    );
}