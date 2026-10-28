using DigitalCap.Core.Models.DataTransfer;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalCap.Core.Interfaces.Repository
{
    /// <summary>
    /// Local (SQLCipher SQLite) side of project download/sync. Always uses its own connection to the local
    /// database, independent of the online/offline unit of work.
    /// </summary>
    public interface IOfflineTransferRepository
    {
        Task<bool> ProjectExistsAsync(int projectId, CancellationToken cancellationToken = default);

        Task<List<int>> GetProjectIdsAsync(CancellationToken cancellationToken = default);

        /// <summary>True when any row of the project has IsSync set (created/modified offline and not yet uploaded).</summary>
        Task<PendingChanges> GetPendingChangesAsync(int projectId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Writes a downloaded project in one transaction. When <paramref name="replaceExisting"/> is true the
        /// existing local copy is first copied to the *_backup tables (scoped to this project) and deleted, so it
        /// can be put back with <see cref="RestoreBackupAsync"/>. Any failure rolls everything back.
        /// </summary>
        Task SaveDownloadedProjectAsync(int projectId, global::TransferDataModel data, bool replaceExisting, string activityUser, CancellationToken cancellationToken = default);

        /// <summary>Replaces the project's rows with its *_backup rows and clears that backup.</summary>
        Task RestoreBackupAsync(int projectId, CancellationToken cancellationToken = default);

        /// <summary>Removes this project's rows from the *_backup tables.</summary>
        Task ClearBackupAsync(int projectId, CancellationToken cancellationToken = default);

        /// <summary>Reference GetTankData: dirty Vessel_TankMapping, H_IReportTemplate, HandIProjectGrading(+Conditions).</summary>
        Task<global::TransferDataModel> GetTankSyncDataAsync(int projectId, CancellationToken cancellationToken = default);

        /// <summary>Reference GetAllSections: (TemplateId, SectionId) pairs having dirty rows.</summary>
        Task<List<DirtySection>> GetDirtySectionsAsync(int projectId, CancellationToken cancellationToken = default);

        /// <summary>Reference GetSQLiteDataByTemplateId: dirty rows of one section (ProjectId set on the model).</summary>
        Task<global::TransferDataModel> GetSectionSyncDataAsync(int projectId, DirtySection section, CancellationToken cancellationToken = default);

        /// <summary>Deletes every local row of the project (reference Release).</summary>
        Task ReleaseAsync(int projectId, CancellationToken cancellationToken = default);

        /// <summary>Marks the project as in-sync (IsSync = NULL) and logs the activity (reference KeepDownloaded).</summary>
        Task KeepDownloadedAsync(int projectId, string activityUser, CancellationToken cancellationToken = default);
    }
}
