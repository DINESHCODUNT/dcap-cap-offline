import React, { useContext, useState } from "react";
import { SideDialog, Button, BUTTONS, SideDialogManagerContext } from "customer_portal-ui-shared";
import * as Icon from "react-bootstrap-icons";
import ImageAttachment from "./ImageAttachment";
import * as surveyReportService from "../../service/SurveyReportService";
import { usePopup } from '../../Common/PopupProvider';

const BulkImageUpload: React.FC<{
    id: string,
    fileNameOptions: any,
    descriptionOptions: any,
    reportSectionOptions: any,
    conditionOptions: any,
    projectId: number,
    templateId: number,
    data: any,
    onUploadSuccess: any,
    isNewProject?: boolean,
    readOnly?: boolean
}> = ({ id, fileNameOptions, descriptionOptions, reportSectionOptions, conditionOptions, projectId, templateId, onUploadSuccess, isNewProject, readOnly = false }) => {

    const sideDialog = useContext(SideDialogManagerContext) as any;
    const popup = usePopup();
    const [attachmentList, setAttachmentList] = useState<any[]>([]);
    const [errors, setErrors] = useState<any>({});
    const [isUploading, setIsUploading] = useState(false);
    const handleClose = () => {
        sideDialog.removeDialog(id);
    };
    const validateRows = () => {
        const newErrors: any = {};

        if (attachmentList.length === 0) {
            newErrors["emptyList"] = "Please add at least one image before uploading.";
            setErrors(newErrors);
            return false;
        }

        attachmentList.forEach((row) => {
            if (!row.file) {
                newErrors[`file_${row.id}`] = "File is required";
            }
            if (!row.fileName) {
                newErrors[`imageCard_${row.id}`] = "Image Card required";
            }
            if (!row.reportSection) {
                newErrors[`section_${row.id}`] = "Report Section required";
            }
            if (!row.description) {
                newErrors[`description_${row.id}`] = "Image Description required";
            }

            const selectedFileOpt = fileNameOptions.find((o: any) => String(o.value) === String(row.fileName));
            const isSelectableCondition = selectedFileOpt?.currentCondition;
            const isCurrentCondition =
                isSelectableCondition === 0 ||
                isSelectableCondition === "0" ||
                isSelectableCondition === false ||
                isSelectableCondition === "false";

            if (!isCurrentCondition && !row.condition) {
                newErrors[`condition_${row.id}`] = "File Name Required";
            } else {
                const selectedDesc = descriptionOptions.find((d: any) => String(d.value) === String(row.description));
                if (selectedDesc?.text?.toLowerCase()?.includes("other") && !row.additionalDescription?.trim()) {
                    newErrors[`additionalDescription_${row.id}`] = "Additional Description required";
                }
            }
        });

        setErrors(newErrors);

        return Object.keys(newErrors).length === 0;
    };

    const handleBulkUpload = async () => {
        if (readOnly) return;
        if (!validateRows()) return;

        setIsUploading(true);

        try {

            for (const row of attachmentList) {

                const formData = new FormData();

                formData.append("file", row.file);

                const cleanSectionId = row.reportSection?.includes(":")
                    ? row.reportSection.split(":")[1]
                    : row.reportSection;

                formData.append("projectId", projectId.toString());
                formData.append("templateId", templateId.toString());
                formData.append("sectionId", cleanSectionId);
                formData.append("cardId", row.fileName);
                formData.append("imageId", "0");
                formData.append("replaceImage", "false");

                if (isNewProject) {
                    const selectedFileOpt = fileNameOptions.find((o: any) => String(o.value) === String(row.fileName));
                    const actualCardName = selectedFileOpt?.text || "Unknown";

                    await surveyReportService.uploadGenericSurveyImage({
                        file: row.file,
                        projectId,
                        templateId,
                        sectionId: cleanSectionId,
                        cardId: row.fileName,
                        imageId: 0,
                        replaceImage: false,
                        cardName: actualCardName
                    });
                } else {
                    await surveyReportService.uploadImage(formData);
                }

                if (row.description) {
                    const selectedFileOpt = fileNameOptions.find((o: any) => String(o.value) === String(row.fileName));
                    const actualCardName = selectedFileOpt?.text || "Unknown";

                    if (isNewProject) {
                        await surveyReportService.updateGenericImageDescriptionDropdownCard({
                            templateId,
                            projectId,
                            sectionId: cleanSectionId,
                            cardId: row.fileName,
                            value: row.description,
                            cardName: actualCardName
                        });
                    } else {
                        await surveyReportService.updateImageDescriptionDropdownCard({
                            templateId,
                            projectId,
                            sectionId: cleanSectionId,
                            cardId: row.fileName,
                            value: row.description
                        });
                    }

                    const selectedDesc = descriptionOptions.find((d: any) => String(d.value) === String(row.description));
                    if (selectedDesc?.text?.toLowerCase()?.includes("other") && row.additionalDescription) {
                        if (isNewProject) {
                            await surveyReportService.updateGenericAdditionalDescription({
                                templateId,
                                projectId,
                                sectionId: cleanSectionId,
                                cardId: row.fileName,
                                value: row.additionalDescription,
                                cardName: actualCardName
                            });
                        } else {
                            await surveyReportService.updateAdditionalDescription({
                                templateId,
                                projectId,
                                sectionId: cleanSectionId,
                                cardId: row.fileName,
                                value: row.additionalDescription
                            });
                        }
                    }

                    const conditionValue = row.condition ? row.condition : 0;
                    console.log("BULK_UPLOAD_SAVE_CONDITION", {
                        rowCondition: row.condition,
                        conditionValue,
                        isNewProject,
                        templateId,
                        projectId,
                        sectionId: cleanSectionId,
                        cardId: row.fileName,
                        cardName: actualCardName
                    });
                    if (isNewProject) {
                        await surveyReportService.updateGenericCurrentCondition({
                            templateId,
                            projectId,
                            sectionId: cleanSectionId,
                            cardId: row.fileName,
                            value: conditionValue,
                            cardName: actualCardName
                        });
                    } else {
                        await surveyReportService.updateCurrentCondition({
                            templateId,
                            projectId,
                            sectionId: cleanSectionId,
                            cardId: row.fileName,
                            value: conditionValue
                        });
                    }
                }
            }
            await onUploadSuccess();
            sideDialog.removeDialog(id);
            popup.success("File has been uploaded successfully.", "Upload File");
        } catch (err: any) {
            popup.error({ message: err?.message || "Failed to upload images." });
        } finally {
            setIsUploading(false);
        }
    };

    return (
        <SideDialog
            id={id}
            confirmOnClose={false}
            className="right-side-dialog"
            showWarningMinimize={() => false}
            title={
                <div className='side-panel-title'>
                    <h5>Bulk Image Upload</h5>

                    <div className="side-header-actions">
                        <Button
                            className="close-button"
                            onClick={handleClose}
                        >
                            <Icon.X />
                        </Button>
                    </div>
                </div>
            }
            minimizedTitle="Bulk Image Upload"
        >
            {() => (
                <>
                    {errors?.emptyList && (
                        <div className="text-danger small mb-2">
                            {errors.emptyList}
                        </div>
                    )}
                    <ImageAttachment
                        fileNameOptions={fileNameOptions}
                        descriptionOptions={descriptionOptions}
                        reportSectionOptions={reportSectionOptions}
                        conditionOptions={conditionOptions}
                        onChange={setAttachmentList}
                        errors={errors}
                    />

                    <div className="d-flex justify-content-end mt-3 gap-2">
                        <Button
                            onClick={handleBulkUpload}
                            disabled={isUploading || readOnly}
                        >
                            {isUploading ? "Uploading..." : "Upload All"}
                        </Button>
                        <Button
                            variant={BUTTONS.SECONDARY}
                            onClick={handleClose}
                            disabled={isUploading}
                        >
                            Cancel
                        </Button>
                    </div>
                </>
            )}
        </SideDialog>


    );
};

export default BulkImageUpload;