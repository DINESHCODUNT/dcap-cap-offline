using DigitalCap.Core.Models.DataTransfer;
using DigitalCap.Core.Models.Survey;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalCap.Core.Interfaces.Service
{
    /// <summary>Orchestrates project download / sync / release between the online website and the local database.</summary>
    public interface IDataTransferService
    {
        Task<DataTransferResult> DownloadAsync(int projectId, string userId, string userName, bool redownload = false, CancellationToken cancellationToken = default);

        Task<DataTransferResult> SyncAsync(int projectId, bool release, string userName, CancellationToken cancellationToken = default);

        /// <summary>Release without sync: discards the staged/offline changes online, then removes the local copy.</summary>
        Task<DataTransferResult> ReleaseAsync(int projectId, CancellationToken cancellationToken = default);

        Task<bool> IsDownloadedAsync(int projectId, CancellationToken cancellationToken = default);

        Task<List<int>> GetDownloadedProjectsAsync(CancellationToken cancellationToken = default);

        Task<PendingChanges> GetPendingChangesAsync(int projectId, CancellationToken cancellationToken = default);

        Task<OnlineLockStatus> GetOnlineLockStatusAsync(int projectId, string? userId, string? userName, CancellationToken cancellationToken = default);

        Task<List<DropdownList>> GetAvailableOnlineProjectsAsync(string? userId, string? userName, CancellationToken cancellationToken = default);
    }
}
