import React, { useContext, useState, useEffect, useMemo } from 'react';
import { SideDialogManagerContext, BreadcrumbBar, Button, BUTTONS } from "customer_portal-ui-shared";
import CreateProject from '../Project/Createproject';
import _uniqueId from "lodash/uniqueId";
import { getRelativeTime } from '../../utils/dateTime';
import ActivityLog from '../Activity/ActivityLog';
import ProjectLanding from '../Project/ProjectLanding';
import * as projectService from '../../service/ProjectService';
import { useNavigate, useLocation } from 'react-router-dom';

const RecentActivityOverview: React.FC = () => {
    const navigate = useNavigate();
    const sideDialog = useContext(SideDialogManagerContext) as any;

    const [selectedProject, setSelectedProject] = useState<{
        name: string;
        imo: string;
        date: string;
        pct: number;
        status: string;
        id?: number;
    } | null>(null);

    const [activities, setActivities] = useState<any[]>([]);
    const memoizedActivities = useMemo(() => activities, [activities]);
    const permissions: string[] = [];
    const hasGlobalPermission = (perm: string) =>
        permissions.includes(perm);
    const location = useLocation();
    const [filteredProjectId, setFilteredProjectId] = useState<number>(0);
    const [filteredProjectName, setFilteredProjectName] = useState<string>("");

    useEffect(() => {
        const state = location.state as { projectId?: any, projectName?: string } | null;
        const pId = state?.projectId ? Number(state.projectId) : 0;
        setFilteredProjectId(pId);
        setFilteredProjectName(state?.projectName || "");
        loadRecentActivities(pId);
    }, [location.state]);

    const loadRecentActivities = async (projectId: number = 0) => {
        try {
            let response;
            if (projectId > 0) {
                response = await projectService.GetAllActivities2ByProjectId(false, projectId);
            } else {
                response = await projectService.GetRecentActivities2(false, 0);
            }

            const mappedActivities = (response.data || []).map((item: any) => ({
                id: item.id,
                ProjectId: item.projectId,
                VesselProject: item.vesselProject,
                text: item.text,
                time: getRelativeTime(item.changeTime),
                userName: item.changedBy,
                userEmail: item.changedBy,
                type: "modify",
                tag: ""
            }));

            setActivities(mappedActivities);
        } catch (err) {
        }
    };

    const handleEditProject = (projectId: number) => {
        const editId = _uniqueId("sideDialog_");

        sideDialog.addDialog({
            id: editId,
            component: <CreateProject id={editId} projectId={projectId} onSuccess={() => loadRecentActivities(filteredProjectId)} />
        });
    };

    const handleArrowClick = (activity: any) => {
        navigate('/app/Project', {
            state: {
                projectId: activity.ProjectId,
                projectName: activity.VesselProject,
                focusTab: 0
            }
        });
    };

    return (
        <>
            <div className="page-title">
                <BreadcrumbBar pageName={filteredProjectName ? `Recent Activity - ${filteredProjectName}` : "Recent Activity"}>
                    <div className="section-actions d-flex flex-wrap align-items-center gap-2">
                        {filteredProjectId > 0 && (
                            <Button
                                variant={BUTTONS.SECONDARY}
                                onClick={() => {
                                    navigate('/app/Project', {
                                        state: {
                                            projectId: filteredProjectId,
                                            projectName: filteredProjectName,
                                            focusTab: 99
                                        }
                                    });
                                }}
                            >
                                &#8592; Back to Project ({filteredProjectName || 'Project'})
                            </Button>
                        )}
                    </div>
                </BreadcrumbBar>
            </div>
            <div className="page-content p-4 recent-activity-page">
                <div className="row">
                    <div className="col-md-12">
                        <div className="grid-container">
                            <div className="grid-content-body">
                                {selectedProject ? (
                                    <ProjectLanding
                                        project={selectedProject}
                                        onBack={() => setSelectedProject(null)}
                                        onEdit={handleEditProject}
                                        onProjectsChanged={() => {
                                            loadRecentActivities();
                                        }}
                                        globalPermission={hasGlobalPermission}
                                    />
                                ) : (
                                    <div className="card-area">
                                        <ActivityLog
                                            source="recentActivity"
                                            activities={memoizedActivities}
                                            onArrowClick={handleArrowClick}
                                        />
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </>
    );
};

export default RecentActivityOverview;