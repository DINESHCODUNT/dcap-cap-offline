using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace DigitalCap.Core.Security
{
    public enum Permission
    {
        #region ABS Dashboard
        ViewAbsDashboard,
        #endregion

        #region Recent Activity All Projects
        ViewRecentActivityAllProjectsAllRegions,

        ViewRecentActivityAllProjectsOwnRegion,
        #endregion

        #region Contracted Backlog Gantt Chart
        ViewContractedBacklogAllRegions,

        ViewContractedBacklogOwnRegion,

        ViewContractedBacklogOwnProjects,
        #endregion

        #region Survey Trends
        ViewSurveyTrends,
        #endregion

        #region All Projects
        //[Group("All Projects")]
        [Description("View All Projects")]
        ViewAllProjects,

        // [Group("All Projects")]
        [Description("Add Project")]
        AddProject,

        //  [Group("All Projects")]
        [Description("Open Project")]
        OpenProject,

        // [Group("All Projects")]
        [Description("Export Project")]
        ExportProject,
        #endregion

        #region ABS Project Landing Page
        //[Group("ABS Project Landing Page")]
        [Description("View ABS Project Landing Page")]
        ViewAbsProjectLandingPage,

        // [Group("ABS Project Landing Page")]
        [Description("Revenue Tracker")]
        AbsProjectLandingPageRevenueTracker,

        //[Group("ABS Project Landing Page")]
        [Description("Manage My Comments")]
        AbsProjectLandingPageManageMyComments,

        //  [Group("ABS Project Landing Page")]
        [Description("Edit Report Details")]
        AbsProjectLandingPageEditReportDetails,

        // [Group("ABS Project Landing Page")]
        [Description("Cancel Button")]
        AbsProjectLandingPageCancelButton,

        // [Group("ABS Project Landing Page")]
        [Description("Delete Button")]
        AbsProjectLandingPageDeleteButton,

        // [Group("ABS Project Landing Page")]
        [Description("Close Button")]
        AbsProjectLandingPageCloseButton,

        // [Group("ABS Project Landing Page")]
        [Description("Reassessment Button")]
        AbsProjectLandingPageReassessmentButton,

        // [Group("ABS Project Landing Page")]
        [Description("Project Details Link")]
        AbsProjectLandingPageProjectDetailsLink,

        // [Group("ABS Project Landing Page")]
        [Description("Workflow Link")]
        AbsProjectLandingPageWorkflowLink,

        // [Group("ABS Project Landing Page")]
        [Description("Recent Activity Link")]
        AbsProjectLandingPageRecentActivityLink,

        // [Group("ABS Project Landing Page")]
        [Description("Project Files Link")]
        AbsProjectLandingPageProjectFilesLink,

        // [Group("ABS Project Landing Page")]
        [Description("Project Files Link (Only Internal Documents Bucket)")]
        AbsProjectLandingPageProjectFilesLinkOnlyInternalDocumentsBucket,

        // [Group("ABS Project Landing Page")]
        [Description("Report Link")]
        AbsProjectLandingPageReportLink,

        //[Group("ABS Project Landing Page")]
        [Description("Client Deliverable Report Link")]
        AbsProjectLandingPageClientDeliverableReportLink,

        #endregion

        #region Invoicing And Tasks
        // [Group("Invoicing And Tasks")]
        [Description("View Invoicing And Tasks")]
        ViewInvoicingAndTasks,

        // [Group("Invoicing And Tasks")]
        [Description("View Task Page")]
        InvoicingAndTasksViewTaskPage,

        // [Group("Invoicing And Tasks")]
        [Description("Edit Task Details")]
        InvoicingAndTasksEditTaskDetails,

        //  [Group("Invoicing And Tasks")]
        [Description("Manage My Comments")]
        InvoicingAndTasksManageMyComments,

        // [Group("Invoicing And Tasks")]
        [Description("Manage Files")]
        InvoicingAndTasksManageFiles,
        #endregion

        #region Workflow And Tasks
        // [Group("Workflow And Tasks")]
        [Description("View Workflow And Tasks")]
        ViewWorkflowAndTasks,

        // [Group("Workflow And Tasks")]
        [Description("View Task Page")]
        WorkflowAndTasksViewTaskPage,

        //  [Group("Workflow And Tasks")]
        [Description("Edit Task Details")]
        WorkflowAndTasksEditTaskDetails,

        // [Group("Workflow And Tasks")]
        [Description("Manage My Comments")]
        WorkflowAndTasksManageMyComments,

        // [Group("Workflow And Tasks")]
        [Description("Manage Files")]
        WorkflowAndTasksManageFiles,

        // [Group("Workflow And Tasks")]
        [Description("Manage Files (Only Internal Documents Bucket)")]
        WorkflowAndTasksManageFilesOnlyInternalDocumentsBucket,

        #endregion

        #region ABS Project Files
        //  [Group("ABS Project Files")]
        [Description("View ABS Project Files")]
        ViewAbsProjectFiles,

        //  [Group("ABS Project Files")]
        [Description("Add Files")]
        AbsProjectFilesAddFiles,

        //  [Group("ABS Project Files")]
        [Description("Add Files (Only Internal Documents Bucket)")]
        AbsProjectFilesAddFilesOnlyInternalDocumentsBucket,

        // [Group("ABS Project Files")]
        [Description("Delete Files")]
        AbsProjectFilesDeleteFiles,

        //[Group("ABS Project Files")]
        [Description("Delete Files (Only Internal Documents Bucket)")]
        AbsProjectFilesDeleteFilesOnlyInternalDocumentsBucket,

        // [Group("ABS Project Files")]
        [Description("Edit/Publish Files")]
        AbsProjectFilesEditPublishFiles,

        //[Group("ABS Project Files")]
        [Description("Edit/Publish Files (Only Internal Documents Bucket)")]
        AbsProjectFilesEditPublishFilesOnlyInternalDocumentsBucket,

        #endregion

        #region Project Details
        // [Group("Project Details")]
        [Description("View Project Details")]
        ViewProjectDetails,

        // [Group("Project Details")]
        [Description("Edit/Save Project Details")]
        EditSaveProjectDetails,

        #endregion

        #region Project Recent Activity
        // [Group("Project Recent Activity")]
        [Description("View Project Recent Activity")]
        ViewProjectRecentActivity,
        #endregion

        #region Manage Client Profiles
        ViewClientProfiles,

        ManageClientProfiles,

        ManageClientAdmins,

        EditClientProfileDetails,

        #endregion

        #region Manage ABS Users
        ViewAbsUsers,

        ManageAbsUsers,
        #endregion

        #region Client Dashboard
        ViewClientDashboard,

        //  [Group("Client Dashboard")]
        ClientDashboardDownloadDeliverables,
        #endregion

        #region Client Project Landing Page
        ViewClientProjectLandingPage,

        ClientProjectLandingPageDownloadDeliverables,

        ClientProjectLandingPageShareDeliverables,

        ClientProjectLandingPageProjectFilesLink,
        #endregion

        #region Client Project Files
        ViewClientProjectFiles,

        ManageClientProjectFiles,
        #endregion

        #region Manage Client Users
        ViewClientUsers,

        ManageClientUsers,
        #endregion

        #region Special
        SystemAdministrator,

        #endregion
    }
}
