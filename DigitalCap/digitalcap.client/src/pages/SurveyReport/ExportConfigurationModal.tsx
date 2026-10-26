import React, { useState, useEffect } from 'react';
import { Modal, Form, Spinner } from 'react-bootstrap';
import { Button, BUTTONS } from 'customer_portal-ui-shared';
import { getAllSections } from '../../service/SurveyReportService';

interface ExportConfigurationModalProps {
    show: boolean;
    onHide: () => void;
    onExport: (selectedSettings: any) => void;
    projectId: number;
    templateId: number;
    imo: number;
    templateName: string;
    mode?: 'export' | 'validate';
    templateSections?: any[];
}

export const ExportConfigurationModal: React.FC<ExportConfigurationModalProps> = ({
    show,
    onHide,
    onExport,
    projectId,
    templateId,
    imo,
    templateName,
    mode = 'export',
    templateSections
}) => {
    const [sections, setSections] = useState<any[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [isExporting, setIsExporting] = useState(false);

    useEffect(() => {
        if (show) {
            setIsExporting(false);
            loadSections();
        } else {
            setSections([]);
        }
    }, [show, templateId]);

    const loadSections = async () => {
        setIsLoading(true);
        try {
            if (templateSections && templateSections.length > 0) {
                const mappedSections = templateSections.map((s: any) => ({
                    ...s,
                    SectionId: s.sectionId ?? s.SectionId,
                    SectionName: s.sectionName ?? s.SectionName ?? s.name ?? s.Name,
                    IncludeInReport: s.includeInReport ?? s.IncludeInReport ?? true,
                    RequiredInReport: s.requiredInReport ?? s.RequiredInReport ?? false
                }));
                setSections(mappedSections);
                setIsLoading(false);
                return;
            }

            const res = await getAllSections(projectId, templateId, imo);
            if (res.data && (res.data as any[]).length > 0) {
                const sectionsData = res.data as any[] ?? [];
                const mappedSections = sectionsData.map((s: any) => ({
                    ...s,
                    IncludeInReport: s.includeInReport ?? s.IncludeInReport ?? true
                }));
                setSections(mappedSections);
            } else {
                setSections([]);
            }
        } catch (error) {
        } finally {
            setIsLoading(false);
        }
    };

    const handleCheckboxChange = (sectionId: number, checked: boolean) => {
        setSections(prev => prev.map(s => {
            const currentId = s.sectionId ?? s.SectionId;
            if (currentId === sectionId) {
                return { ...s, IncludeInReport: checked };
            }
            return s;
        }));
    };

    const handleExportClick = async () => {
        setIsExporting(true);
        const selectedSettings = sections
            .filter(s => s.IncludeInReport || s.requiredInReport || s.RequiredInReport)
            .map(s => ({
                SectionId: s.sectionId ?? s.SectionId,
                SectionName: s.sectionName ?? s.SectionName,
                IncludeInReport: true,
                RequiredInReport: s.requiredInReport ?? s.RequiredInReport ?? false,
                IsSubSection: false,
                TankTypeId: s.tankTypeId ?? s.TankTypeId ?? 0,
                ParentId: s.parentId ?? s.ParentId ?? "00000000-0000-0000-0000-000000000000",
                TankType: s.tankType ?? s.TankType ?? ""
            }));

        const payload = [{
            PartId: templateId,
            PartName: templateName,
            RequiredInReport: false,
            IncludeInReport: true,
            sections: selectedSettings
        }];

        await onExport(payload);
        setIsExporting(false);
    };

    return (
        <Modal show={show} onHide={onHide} size="lg" centered backdrop="static">
            <Modal.Header closeButton>
                <Modal.Title>{mode === 'validate' ? 'Validate' : 'Export'} {templateName} Sections</Modal.Title>
            </Modal.Header>
            <Modal.Body style={{ maxHeight: '60vh', overflowY: 'auto' }}>
                {isLoading ? (
                    <div className="d-flex justify-content-center align-items-center" style={{ height: '100px' }}>
                        <Spinner animation="border" variant="primary" />
                        <span className="ms-2">Loading sections...</span>
                    </div>
                ) : (
                    <div className="section-list p-2">
                        {sections.length === 0 ? (
                            <p>No sections found.</p>
                        ) : (
                            sections.map(section => {
                                const isRequired = section.requiredInReport || section.RequiredInReport;
                                const sectionId = section.sectionId ?? section.SectionId;
                                const sectionName = section.sectionName ?? section.SectionName;

                                return (
                                    <Form.Check
                                        key={sectionId}
                                        type="checkbox"
                                        id={`section-${sectionId}`}
                                        label={sectionName}
                                        checked={isRequired ? true : section.IncludeInReport}
                                        disabled={isRequired}
                                        onChange={(e) => handleCheckboxChange(sectionId, e.target.checked)}
                                        className="mb-3"
                                    />
                                );
                            })
                        )}
                    </div>
                )}
            </Modal.Body>
            <Modal.Footer className="gap-2">
                <Button variant={BUTTONS.TERTIARY} onClick={onHide} disabled={isExporting}>Close</Button>
                <Button variant={BUTTONS.PRIMARY} onClick={handleExportClick} disabled={isLoading || isExporting}>
                    {isExporting ? (mode === 'validate' ? 'Validating...' : 'Exporting...') : (mode === 'validate' ? 'Validate' : 'Export')}
                </Button>
            </Modal.Footer>
        </Modal>
    );
};
