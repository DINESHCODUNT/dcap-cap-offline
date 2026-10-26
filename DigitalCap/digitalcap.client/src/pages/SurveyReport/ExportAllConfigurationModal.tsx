import React, { useState, useEffect } from 'react';
import { Modal, Form, Spinner, Accordion } from 'react-bootstrap';
import { Button, BUTTONS } from 'customer_portal-ui-shared';
import { getAllParts } from '../../service/SurveyReportService';

interface ExportAllConfigurationModalProps {
    show: boolean;
    onHide: () => void;
    onExport: (selectedSettings: any) => void;
    projectId: number;
    imo: number;
    projectParts?: any[];
}

export const ExportAllConfigurationModal: React.FC<ExportAllConfigurationModalProps> = ({
    show,
    onHide,
    onExport,
    projectId,
    imo,
    projectParts
}) => {
    const [parts, setParts] = useState<any[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [isExporting, setIsExporting] = useState(false);

    useEffect(() => {
        if (show) {
            setIsExporting(false);
            loadParts();
        } else {
            setParts([]);
        }
    }, [show, projectParts]);

    const loadParts = async () => {
        setIsLoading(true);
        try {
            if (projectParts && projectParts.length > 0) {
                const partsData = projectParts
                    .filter((p: any) => p.templateId !== 0)
                    .map((p: any) => ({
                        PartId: p.templateId,
                        PartName: p.name,
                        IncludeInReport: true,
                        sections: (p.sectionsList ?? []).map((s: any) => ({
                            SectionId: s.sectionId ?? s.SectionId,
                            SectionName: s.sectionName ?? s.SectionName ?? s.name ?? s.Name,
                            IncludeInReport: true
                        }))
                    }));
                setParts(partsData);
                setIsLoading(false);
                return;
            }

            const res = await getAllParts(projectId, imo);
            if (res.data) {
                const partsData = (res.data as any[]).map(p => ({
                    ...p,
                    IncludeInReport: p.includeInReport ?? p.IncludeInReport ?? true,
                    sections: (p.sections ?? p.Sections ?? []).map((s: any) => ({
                        ...s,
                        IncludeInReport: s.includeInReport ?? s.IncludeInReport ?? true
                    }))
                }));
                setParts(partsData);
            }
        } catch (error) {
        } finally {
            setIsLoading(false);
        }
    };

    const handlePartCheckboxChange = (partId: number, checked: boolean) => {
        setParts(prev => prev.map(p => {
            const currentPartId = p.partId ?? p.PartId;
            if (currentPartId === partId) {
                return {
                    ...p,
                    IncludeInReport: checked,
                    sections: p.sections.map((s: any) => ({
                        ...s,
                        IncludeInReport: checked
                    }))
                };
            }
            return p;
        }));
    };

    const handleSectionCheckboxChange = (partId: number, sectionId: string, checked: boolean) => {
        setParts(prev => prev.map(p => {
            const currentPartId = p.partId ?? p.PartId;
            if (currentPartId === partId) {
                const updatedSections = p.sections.map((s: any) => {
                    const currentSectionId = s.sectionId ?? s.SectionId;
                    if (currentSectionId === sectionId) {
                        return { ...s, IncludeInReport: checked };
                    }
                    return s;
                });

                const anyChecked = updatedSections.some((s: any) => s.IncludeInReport);
                return {
                    ...p,
                    IncludeInReport: anyChecked,
                    sections: updatedSections
                };
            }
            return p;
        }));
    };

    const handleExportClick = async () => {
        setIsExporting(true);
        const payload = parts.map(p => {
            const selectedSections = p.sections
                .filter((s: any) => s.IncludeInReport || s.requiredInReport || s.RequiredInReport)
                .map((s: any) => ({
                    SectionId: s.sectionId ?? s.SectionId,
                    SectionName: s.sectionName ?? s.SectionName,
                    IncludeInReport: true,
                    RequiredInReport: s.requiredInReport ?? s.RequiredInReport ?? false,
                    IsSubSection: false,
                    TankTypeId: s.tankTypeId ?? s.TankTypeId ?? 0,
                    ParentId: s.parentId ?? s.ParentId ?? "00000000-0000-0000-0000-000000000000",
                    TankType: s.tankType ?? s.TankType ?? ""
                }));

            return {
                PartId: p.partId ?? p.PartId,
                PartName: p.partName ?? p.PartName,
                RequiredInReport: p.requiredInReport ?? p.RequiredInReport ?? false,
                IncludeInReport: p.IncludeInReport,
                sections: selectedSections
            };
        });

        await onExport(payload);
        setIsExporting(false);
    };

    return (
        <Modal show={show} onHide={onHide} size="lg" centered backdrop="static">
            <Modal.Header closeButton>
                <Modal.Title>Export All Configurations</Modal.Title>
            </Modal.Header>
            <Modal.Body style={{ maxHeight: '60vh', overflowY: 'auto' }}>
                {isLoading ? (
                    <div className="d-flex justify-content-center align-items-center" style={{ height: '100px' }}>
                        <Spinner animation="border" variant="primary" />
                        <span className="ms-2">Loading report configuration...</span>
                    </div>
                ) : (
                    <Accordion className="p-2">
                        {parts.length === 0 ? (
                            <p>No report sections found.</p>
                        ) : (
                            parts.map((part, index) => {
                                const partId = part.partId ?? part.PartId;
                                const partName = part.partName ?? part.PartName;

                                return (
                                    <Accordion.Item eventKey={String(index)} key={partId} className="mb-2">
                                        <Accordion.Header>
                                            <div className="d-flex align-items-center" onClick={(e) => e.stopPropagation()}>
                                                <Form.Check
                                                    type="checkbox"
                                                    id={`part-${partId}`}
                                                    checked={part.IncludeInReport}
                                                    onChange={(e) => handlePartCheckboxChange(partId, e.target.checked)}
                                                    className="me-3"
                                                />
                                                <span>{partName}</span>
                                            </div>
                                        </Accordion.Header>
                                        <Accordion.Body>
                                            {part.sections.length === 0 ? (
                                                <p className="text-muted">No sections in this part.</p>
                                            ) : (
                                                part.sections.map((section: any) => {
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
                                                            onChange={(e) => handleSectionCheckboxChange(partId, sectionId, e.target.checked)}
                                                            className="mb-3"
                                                        />
                                                    );
                                                })
                                            )}
                                        </Accordion.Body>
                                    </Accordion.Item>
                                );
                            })
                        )}
                    </Accordion>
                )}
            </Modal.Body>
            <Modal.Footer className="gap-2">
                <Button variant={BUTTONS.TERTIARY} onClick={onHide} disabled={isExporting}>Close</Button>
                <Button variant={BUTTONS.PRIMARY} onClick={handleExportClick} disabled={isLoading || isExporting}>
                    {isExporting ? 'Exporting...' : 'Export'}
                </Button>
            </Modal.Footer>
        </Modal>
    );
};
