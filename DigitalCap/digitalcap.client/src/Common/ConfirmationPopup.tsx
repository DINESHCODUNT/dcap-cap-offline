import React from "react";
import Modal from 'react-bootstrap/Modal';
import { Button, BUTTONS } from 'customer_portal-ui-shared';

interface ConfirmationPopupProps {

    onShow: boolean;

    title?: string;

    message?: string;

    confirmText?: string;

    cancelText?: string;

    loading?: boolean;

    onConfirm: () => void;

    onClose: () => void;
}

const ConfirmationPopup: React.FC<ConfirmationPopupProps> = ({

    onShow,

    title = "Confirmation",

    message = "Are you sure you want to continue?",

    confirmText = "Confirm",

    cancelText = "Cancel",

    loading = false,

    onConfirm,

    onClose

}) => {

    return (

        <Modal
            size="lg"
            show={onShow}
            onHide={onClose}
            centered
            aria-labelledby="confirmation-popup"
        >

            {/* HEADER */}

            <Modal.Header closeButton>

                <Modal.Title>
                    {title}
                </Modal.Title>

            </Modal.Header>

            {/* BODY */}

            <Modal.Body>

                <div className="row form-element">

                    <div className="col-md-12">

                        <div className="sub-title">

                            <h5>
                                {message}
                            </h5>

                        </div>

                    </div>

                </div>

            </Modal.Body>

            {/* FOOTER */}

            <Modal.Footer>

                <div className="d-flex justify-content-end fixed-action-btns gap-2">

                    <Button
                        onClick={onClose}
                        disabled={loading}
                        variant={BUTTONS.SECONDARY}
                    >
                        {cancelText}
                    </Button>

                    <Button
                        disabled={loading}
                        onClick={onConfirm}
                    >
                        {loading
                            ? "Please wait..."
                            : confirmText}
                    </Button>

                </div>

            </Modal.Footer>

        </Modal>

    );
};

export default ConfirmationPopup;