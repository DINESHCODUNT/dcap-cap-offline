using DigitalCap.Core.Models.Survey;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalCap.Core.Interfaces.Repository
{
    /// <summary>
    /// Online side of project download/sync: the reads and writes the desktop app makes through the
    /// online server's api/DataTransferDevAPI.
    /// </summary>
    public interface IOnlineTransferRepository
    {
        /// <summary>dbo.GetDownloadOfflineProject — the "downloaded for offline" marker, or null when none.</summary>
        Task<IsSynchedOnline?> GetDownloadedProjectAsync(int projectId, CancellationToken cancellationToken = default);

        /// <summary>dbo.AddDownloadOfflineProject (reference InsertDownloadedProject).</summary>
        Task AddDownloadedProjectAsync(int projectId, string userId, string userName, CancellationToken cancellationToken = default);

        /// <summary>Reference GetUnChangedDataByTemplateId. Returns null when the project does not exist online.</summary>
        Task<global::TransferDataModel?> GetProjectDataAsync(int projectId, CancellationToken cancellationToken = default);

        /// <summary>
        /// [CAP].[Read_Cap2Tasks_ByProjectId]: the project's CAP2 workflow tasks, kept locally as a read-only
        /// snapshot so the offline landing page can show the workflow (desktop addition; not in the reference download).
        /// </summary>
        Task<List<DigitalCap.Core.Models.SingleTask>> GetProjectTasksAsync(int projectId, CancellationToken cancellationToken = default);

        /// <summary>
        /// The user's roles (dbo.GetCurrentUserRole), permission claims (AspNetRoleClaims/AspNetUserClaims) and the
        /// role-page permission matrix (dbo.GetPermissionOffline) — the data the reference fetched through
        /// DataTransferDevAPI GetCurrentUserRoles / GetRolePermissions to evaluate permissions offline.
        /// </summary>
        Task<DigitalCap.Core.Models.DataTransfer.OfflineUserSecurity> GetUserSecurityAsync(string userName, CancellationToken cancellationToken = default);

        /// <summary>[dbo].[GetAllSectionByProjectId] (reference GetSectionsById).</summary>
        Task<List<AllSections>> GetAllSectionsAsync(int projectId, CancellationToken cancellationToken = default);

        /// <summary>Reference GetDataByTemplateId (cards, gradings, notes, images of one section).</summary>
        Task<global::TransferDataModel> GetSectionDataAsync(int projectId, int templateId, Guid sectionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reference InsertSyncData: inserts the batch into the *_offline staging tables and runs
        /// dbo.MoveToHistoryTable(model.ProjectId), all in one SQL transaction.
        /// </summary>
        Task InsertSyncDataAsync(global::TransferDataModel model, CancellationToken cancellationToken = default);

        /// <summary>[dbo].[MoveToOnlineTable](ProjectId, Keep).</summary>
        Task MoveToOnlineTableAsync(int projectId, int keep, CancellationToken cancellationToken = default);

        /// <summary>dbo.ReleaseSyncData (reference DeleteSyncData — release without sync).</summary>
        Task ReleaseSyncDataAsync(int projectId, CancellationToken cancellationToken = default);

        /// <summary>dbo.ReleaseErrorSyncData (reference ReleasedAPIStoppedData — clears a failed/partial upload).</summary>
        Task ReleaseErrorSyncDataAsync(int projectId, CancellationToken cancellationToken = default);

        /// <summary>Reference GetAllDropDownProjects: gets projects available to download.</summary>
        Task<List<DropdownList>> GetAllDropDownProjectsAsync(string? userId, string? userName, CancellationToken cancellationToken = default);
    }
}
