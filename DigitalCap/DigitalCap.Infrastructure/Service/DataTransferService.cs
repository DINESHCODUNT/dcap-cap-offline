using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models.DataTransfer;
using DigitalCap.Core.Models.Survey;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalCap.Infrastructure.Service
{
    /// <summary>
    /// Server-side port of the download / sync / release orchestration that the reference desktop app ran in
    /// JavaScript (digital-cap-offline Views/Shared/_Layout.cshtml: downloadprocessing, DataSyncing,
    /// handleSyncSuccess/handleSyncError, ReleaseWithoutSync), keeping the same order of server calls.
    /// </summary>
    public class DataTransferService : IDataTransferService
    {
        /// <summary>
        /// The reference uploads the tank batch (GetTankData -> InsertSyncData) without setting ProjectId, so
        /// dbo.MoveToHistoryTable runs with 0 for that batch. Kept as-is because the SP body is not available.
        /// </summary>
        private const int TankBatchProjectId = 0;

        // One transfer per project at a time (double clicks, two windows).
        private static readonly ConcurrentDictionary<int, SemaphoreSlim> ProjectLocks = new();

        private readonly IOnlineTransferRepository _online;
        private readonly IOfflineTransferRepository _offline;
        private readonly ITransferProgressStore _progressStore;
        private readonly ILogger<DataTransferService> _logger;

        public DataTransferService(IOnlineTransferRepository online, IOfflineTransferRepository offline, ITransferProgressStore progressStore, ILogger<DataTransferService> logger)
        {
            _online = online;
            _offline = offline;
            _progressStore = progressStore;
            _logger = logger;
        }

        // ================================================================ download

        public async Task<DataTransferResult> DownloadAsync(int projectId, string userId, string userName, bool redownload = false, CancellationToken cancellationToken = default)
        {
            if (!IsOnline.OnlineOrOffline)
                return DataTransferResult.Fail(DataTransferStatus.Offline, "You are offline. Connect to the internet to download the project.");

            var gate = ProjectLocks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
            if (!await gate.WaitAsync(0, cancellationToken))
                return DataTransferResult.Fail(DataTransferStatus.Conflict, "A download or sync of this project is already in progress.");
            try
            {
                return await DownloadCoreAsync(projectId, userId, userName, redownload, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<DataTransferResult> DownloadCoreAsync(int projectId, string userId, string userName, bool redownload, CancellationToken ct)
        {
            _progressStore.Set(projectId, 0);
            int done = 0;
            int total = 0;
            void ReportProgress()
            {
                if (total > 0)
                {
                    var pct = Math.Min(99, (int)Math.Round(done * 100.0 / total));
                    _progressStore.Set(projectId, pct);
                }
            }

            // 1. Server marker: someone else holding the project offline blocks the download.
            IsSynchedOnline? marker;
            try
            {
                marker = await _online.GetDownloadedProjectAsync(projectId, ct);
            }
            catch (Exception ex)
            {
                _progressStore.Remove(projectId);
                _logger.LogError(ex, "Download {ProjectId}: reading the download marker failed", projectId);
                return DataTransferResult.Fail(DataTransferStatus.Error, "Could not check whether the project is already downloaded: " + ex.Message);
            }

            var markerIsMine = marker != null && IsMarkerOwner(marker, userId, userName);
            if (marker != null && !markerIsMine)
            {
                _progressStore.Remove(projectId);
                var owner = ToLockOwner(marker);
                return new DataTransferResult
                {
                    Status = DataTransferStatus.Conflict,
                    Message = $"This project is already downloaded for offline use by {owner.Name ?? owner.Email ?? "another user"}.",
                    LockedBy = owner
                };
            }

            // 2. Existing local copy: re-download only when it has nothing waiting to be uploaded.
            bool existsLocally;
            try
            {
                existsLocally = await _offline.ProjectExistsAsync(projectId, ct);
                if (existsLocally && !redownload)
                {
                    var pending = await _offline.GetPendingChangesAsync(projectId, ct);
                    if (pending.HasChanges)
                    {
                        _progressStore.Remove(projectId);
                        return DataTransferResult.Fail(DataTransferStatus.Conflict,
                            "This project has offline changes that have not been synced. Sync first, or release it to discard the changes, before downloading again.");
                    }
                }
            }
            catch (Exception ex)
            {
                _progressStore.Remove(projectId);
                _logger.LogError(ex, "Download {ProjectId}: reading the local database failed", projectId);
                return DataTransferResult.Fail(DataTransferStatus.Error, "Could not read the local database: " + ex.Message);
            }

            done = 1;

            // 3. Read everything from Azure SQL before touching the local database.
            TransferDataModel data;
            try
            {
                var project = await _online.GetProjectDataAsync(projectId, ct);
                if (project == null)
                {
                    _progressStore.Remove(projectId);
                    return DataTransferResult.Fail(DataTransferStatus.Error, $"Project {projectId} was not found online.");
                }
                data = project;
                done = 2;

                var sections = (await _online.GetAllSectionsAsync(projectId, ct))
                    .GroupBy(s => (s.TemplateId, s.SectionId))
                    .Select(g => g.First())
                    .ToList();
                done = 3;
                total = sections.Count + 4;
                ReportProgress();

                foreach (var section in sections)
                {
                    var sectionData = await _online.GetSectionDataAsync(projectId, section.TemplateId, section.SectionId, ct);
                    MergeSection(data, sectionData);
                    done++;
                    ReportProgress();
                }

                // Desktop additions stored with the project: workflow tasks for the offline landing page, and the
                // user's roles/permissions so permission checks work offline (the reference kept the latter in
                // the browser's localStorage, fetched from DataTransferDevAPI GetRolePermissions/GetCurrentUserRoles).
                data.ProjectTasks = await _online.GetProjectTasksAsync(projectId, ct);
                data.UserSecurity = await _online.GetUserSecurityAsync(userName, ct);
                done++;
                ReportProgress();
            }
            catch (Exception ex)
            {
                _progressStore.Remove(projectId);
                _logger.LogError(ex, "Download {ProjectId}: reading project data from Azure SQL failed", projectId);
                return DataTransferResult.Fail(DataTransferStatus.Error, "Downloading the project data failed: " + ex.Message);
            }

            // 4. Write it locally in one transaction (re-download backs up and replaces the old copy).
            try
            {
                await _offline.SaveDownloadedProjectAsync(projectId, data, replaceExisting: existsLocally, activityUser: userName, ct);
            }
            catch (Exception ex)
            {
                _progressStore.Remove(projectId);
                _logger.LogError(ex, "Download {ProjectId}: writing to the local database failed", projectId);
                return DataTransferResult.Fail(DataTransferStatus.Error, "Saving the project to the local database failed: " + ex.Message);
            }

            // 5. Record the download online (unless the marker is already ours); undo locally if that fails.
            if (!markerIsMine)
            {
                try
                {
                    await _online.AddDownloadedProjectAsync(projectId, userId, userName, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _progressStore.Remove(projectId);
                    _logger.LogError(ex, "Download {ProjectId}: writing the download marker failed; undoing the local copy", projectId);
                    var undone = await UndoLocalDownloadAsync(projectId, existsLocally);
                    return DataTransferResult.Fail(DataTransferStatus.Error,
                        "Could not register the download on the server: " + ex.Message +
                        (undone ? string.Empty : " The local copy could not be cleaned up; release the project before retrying."));
                }
            }

            if (existsLocally)
            {
                try
                {
                    await _offline.ClearBackupAsync(projectId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // The new copy is complete; a leftover backup is only cleared again on the next re-download.
                    _logger.LogWarning(ex, "Download {ProjectId}: clearing the backup tables failed", projectId);
                }
            }

            _progressStore.Set(projectId, 100);
            _ = Task.Delay(5000).ContinueWith(_ => _progressStore.Remove(projectId));

            return DataTransferResult.Ok(existsLocally ? "Project downloaded again for offline use." : "Project downloaded for offline use.");
        }

        private async Task<bool> UndoLocalDownloadAsync(int projectId, bool wasReDownload)
        {
            try
            {
                if (wasReDownload)
                    await _offline.RestoreBackupAsync(projectId, CancellationToken.None);
                else
                    await _offline.ReleaseAsync(projectId, CancellationToken.None);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Download {ProjectId}: undoing the local copy failed", projectId);
                return false;
            }
        }

        private static void MergeSection(TransferDataModel target, TransferDataModel section)
        {
            target.gradingGradingMapping = Concat(target.gradingGradingMapping, section.gradingGradingMapping);
            target.projectGradingConditionMappings = Concat(target.projectGradingConditionMappings, section.projectGradingConditionMappings);
            target.reportMapping = Concat(target.reportMapping, section.reportMapping);
            target.projectCardMappings = Concat(target.projectCardMappings, section.projectCardMappings);
            target.genericImageCard = Concat(target.genericImageCard, section.genericImageCard);
            target.tankImageCards = Concat(target.tankImageCards, section.tankImageCards);
            target.images = Concat(target.images, section.images);
            target.upskillImageData = Concat(target.upskillImageData, section.upskillImageData);
        }

        private static List<T> Concat<T>(List<T>? first, List<T>? second)
        {
            var result = first ?? new List<T>();
            if (second != null)
                result.AddRange(second);
            return result;
        }

        // ================================================================ sync

        public async Task<DataTransferResult> SyncAsync(int projectId, bool release, string userName, CancellationToken cancellationToken = default)
        {
            if (!IsOnline.OnlineOrOffline)
                return DataTransferResult.Fail(DataTransferStatus.Offline, "You are offline. Connect to the internet to sync the project.");

            var gate = ProjectLocks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
            if (!await gate.WaitAsync(0, cancellationToken))
                return new DataTransferResult { Status = DataTransferStatus.Error, Message = "A download or sync of this project is already in progress." };
            try
            {
                return await SyncCoreAsync(projectId, release, userName, cancellationToken);
            }
            finally
            {
                gate.Release();
            }
        }

        private async Task<DataTransferResult> SyncCoreAsync(int projectId, bool release, string userName, CancellationToken ct)
        {
            _progressStore.Set(projectId, 0);
            try
            {
                if (!await _offline.ProjectExistsAsync(projectId, ct))
                {
                    _progressStore.Remove(projectId);
                    return DataTransferResult.Fail(DataTransferStatus.NotFound, "This project is not downloaded on this device.");
                }
            }
            catch (Exception ex)
            {
                _progressStore.Remove(projectId);
                _logger.LogError(ex, "Sync {ProjectId}: reading the local database failed", projectId);
                return new DataTransferResult { Status = DataTransferStatus.Error, Message = "Could not read the local database: " + ex.Message };
            }

            // Safety net: verify the online download lock before uploading anything.
            try
            {
                var marker = await _online.GetDownloadedProjectAsync(projectId, ct);
                if (marker == null)
                {
                    _progressStore.Remove(projectId);
                    return DataTransferResult.Fail(DataTransferStatus.Conflict, "CAP Administrator has cancelled the project from offline download.  You may contact CAP HQ for further queries.");
                }
            }
            catch (Exception ex)
            {
                _progressStore.Remove(projectId);
                _logger.LogError(ex, "Sync {ProjectId}: checking online download lock failed", projectId);
                return new DataTransferResult { Status = DataTransferStatus.Error, Message = "Could not check online project status: " + ex.Message };
            }

            // Upload (reference DataSyncing). Nothing is final online until MoveToOnlineTable succeeds.
            try
            {
                // Clear whatever a previous failed attempt left in the staging tables (reference calls
                // ReleasedErrorData before showing the sync confirmation).
                await _online.ReleaseErrorSyncDataAsync(projectId, ct);

                var sections = await _offline.GetDirtySectionsAsync(projectId, ct);
                int total = sections.Count + 2;
                int done = 0;

                var tank = await _offline.GetTankSyncDataAsync(projectId, ct);
                tank.ProjectId = TankBatchProjectId;
                await _online.InsertSyncDataAsync(tank, ct);
                done = 1;
                _progressStore.Set(projectId, Math.Min(99, (int)Math.Round(done * 100.0 / total)));

                foreach (var section in sections)
                {
                    var batch = await _offline.GetSectionSyncDataAsync(projectId, section, ct);
                    batch.ProjectId = projectId;
                    await _online.InsertSyncDataAsync(batch, ct);
                    done++;
                    _progressStore.Set(projectId, Math.Min(99, (int)Math.Round(done * 100.0 / total)));
                }

                // Mirrors the reference client exactly: keep = isRelease ? 1 : 0.
                await _online.MoveToOnlineTableAsync(projectId, release ? 1 : 0, ct);
                _progressStore.Set(projectId, 100);
                _ = Task.Delay(5000).ContinueWith(_ => _progressStore.Remove(projectId));
            }
            catch (Exception ex)
            {
                _progressStore.Remove(projectId);
                _logger.LogError(ex, "Sync {ProjectId}: upload failed", projectId);
                try
                {
                    await _online.ReleaseErrorSyncDataAsync(projectId, CancellationToken.None);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx, "Sync {ProjectId}: ReleaseErrorSyncData after failure also failed", projectId);
                }
                return new DataTransferResult
                {
                    Status = DataTransferStatus.Error,
                    Uploaded = false,
                    Message = "Sync failed; nothing was changed online and your offline changes are kept. " + ex.Message
                };
            }

            // Local follow-up (reference handleSyncSuccess).
            try
            {
                if (release)
                    await _offline.ReleaseAsync(projectId, CancellationToken.None);
                else
                    await _offline.KeepDownloadedAsync(projectId, userName, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _progressStore.Remove(projectId);
                _logger.LogError(ex, "Sync {ProjectId}: server sync succeeded but the local {Step} failed", projectId, release ? "release" : "keep");
                return new DataTransferResult
                {
                    Status = DataTransferStatus.Error,
                    Uploaded = true,
                    Message = release
                        ? "Your changes were synced online, but removing the local copy failed: " + ex.Message
                        : "Your changes were synced online, but marking the local copy as synced failed (do not sync it again before re-downloading): " + ex.Message
                };
            }

            return new DataTransferResult
            {
                Status = DataTransferStatus.Ok,
                Uploaded = true,
                Released = release,
                Message = release ? "Project synced and released." : "Project synced; the offline copy is kept."
            };
        }

        // ================================================================ release without sync

        public async Task<DataTransferResult> ReleaseAsync(int projectId, CancellationToken cancellationToken = default)
        {
            if (!IsOnline.OnlineOrOffline)
                return DataTransferResult.Fail(DataTransferStatus.Offline, "You are offline. Connect to the internet to release the project.");

            var gate = ProjectLocks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));
            if (!await gate.WaitAsync(0, cancellationToken))
                return DataTransferResult.Fail(DataTransferStatus.Error, "A download or sync of this project is already in progress.");
            try
            {
                // Reference ReleaseWithoutSync: DeleteSyncData (dbo.ReleaseSyncData) then local release.
                try
                {
                    await _online.ReleaseSyncDataAsync(projectId, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Release {ProjectId}: dbo.ReleaseSyncData failed", projectId);
                    return DataTransferResult.Fail(DataTransferStatus.Error, "Releasing the project on the server failed: " + ex.Message);
                }

                try
                {
                    await _offline.ReleaseAsync(projectId, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Release {ProjectId}: removing the local copy failed", projectId);
                    return DataTransferResult.Fail(DataTransferStatus.Error, "The project was released on the server, but removing the local copy failed: " + ex.Message);
                }

                return DataTransferResult.Ok("Project released.");
            }
            finally
            {
                gate.Release();
            }
        }

        // ================================================================ queries

        public Task<bool> IsDownloadedAsync(int projectId, CancellationToken cancellationToken = default)
            => _offline.ProjectExistsAsync(projectId, cancellationToken);

        public Task<List<int>> GetDownloadedProjectsAsync(CancellationToken cancellationToken = default)
            => _offline.GetProjectIdsAsync(cancellationToken);

        public Task<PendingChanges> GetPendingChangesAsync(int projectId, CancellationToken cancellationToken = default)
            => _offline.GetPendingChangesAsync(projectId, cancellationToken);

        public async Task<OnlineLockStatus> GetOnlineLockStatusAsync(int projectId, string? userId, string? userName, CancellationToken cancellationToken = default)
        {
            var status = new OnlineLockStatus();
            try
            {
                status.IsLocal = await _offline.ProjectExistsAsync(projectId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lock status {ProjectId}: reading the local database failed", projectId);
            }

            if (!IsOnline.OnlineOrOffline)
                return status;

            try
            {
                var marker = await _online.GetDownloadedProjectAsync(projectId, cancellationToken);
                status.IsOnline = true;
                if (marker != null)
                {
                    status.LockedBy = ToLockOwner(marker);
                    status.LockedByMe = IsMarkerOwner(marker, userId, userName);
                }
            }
            catch (Exception ex)
            {
                // Treat an unreadable server as offline so the client does not report a false "discarded".
                _logger.LogWarning(ex, "Lock status {ProjectId}: reading the download marker failed", projectId);
                status.IsOnline = false;
                status.LockedBy = null;
                status.LockedByMe = false;
            }
            return status;
        }

        // ================================================================ helpers

        private static LockOwner ToLockOwner(IsSynchedOnline marker)
        {
            // dbo.AddDownloadOfflineProject stores the email in @UserName; EmailId/Name are mapped when returned.
            var email = FirstNonEmpty(marker.UserName, marker.EmailId);
            return new LockOwner { Email = email, Name = FirstNonEmpty(marker.Name) };
        }

        private static bool IsMarkerOwner(IsSynchedOnline marker, string? userId, string? userName)
        {
            var email = FirstNonEmpty(marker.UserName, marker.EmailId);
            if (email != null && userName != null)
                return string.Equals(email.Trim(), userName.Trim(), StringComparison.OrdinalIgnoreCase);
            if (email == null && !string.IsNullOrWhiteSpace(marker.UserId) && !string.IsNullOrWhiteSpace(userId))
                return string.Equals(marker.UserId.Trim(), userId.Trim(), StringComparison.OrdinalIgnoreCase);
            return false;
        }

        private static string? FirstNonEmpty(params string?[] values)
            => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

        public async Task<List<DropdownList>> GetAvailableOnlineProjectsAsync(string? userId, string? userName, CancellationToken cancellationToken = default)
        {
            if (!IsOnline.OnlineOrOffline)
                return new List<DropdownList>();

            try
            {
                var onlineProjects = await _online.GetAllDropDownProjectsAsync(userId, userName, cancellationToken);
                var downloadedIds = (await _offline.GetProjectIdsAsync(cancellationToken)).ToHashSet();
                return onlineProjects.Where(p => !downloadedIds.Contains(p.ProjectId)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load available online projects");
                return new List<DropdownList>();
            }
        }
    }
}
