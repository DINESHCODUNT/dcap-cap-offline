using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models.DataTransfer;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.WebApi.Controllers;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DigitalCap.Server.Controllers
{
    /// <summary>
    /// Project download / sync / release for the desktop app (replaces the reference
    /// digital-cap-offline DataTransferAPIController + the JavaScript orchestration in _Layout.cshtml).
    /// </summary>
    [Route("api/DataTransferAPI")]
    [EnableCors("AllowSpecificOrigin")]
    public class DataTransferAPIController : ApiBaseController
    {
        private readonly IDataTransferService _dataTransferService;
        private readonly ILogger<DataTransferAPIController> _logger;

        public DataTransferAPIController(IDataTransferService dataTransferService, ILogger<DataTransferAPIController> logger)
        {
            _dataTransferService = dataTransferService;
            _logger = logger;
        }

        private string? CurrentUserObjectId =>
            User.FindFirstValue("oid")
            ?? User.FindFirstValue("http://schemas.microsoft.com/identity/claims/objectidentifier");

        [HttpPost("Download")]
        public async Task<IActionResult> Download([FromQuery] int projectId, [FromQuery] bool redownload = false, CancellationToken cancellationToken = default)
        {
            var userName = CurrentUsername;
            if (string.IsNullOrWhiteSpace(userName))
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "The signed-in user's email could not be determined." });

            DataTransferResult result;
            try
            {
                result = await _dataTransferService.DownloadAsync(projectId, CurrentUserObjectId ?? string.Empty, userName, redownload, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Download {ProjectId} failed unexpectedly", projectId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Download failed: " + ex.Message });
            }

            return result.Status switch
            {
                DataTransferStatus.Ok => Ok(new { success = true, message = result.Message }),
                DataTransferStatus.Conflict when result.LockedBy != null => Conflict(new
                {
                    success = false,
                    message = result.Message,
                    lockedBy = new { name = result.LockedBy.Name, email = result.LockedBy.Email }
                }),
                DataTransferStatus.Conflict => Conflict(new { success = false, message = result.Message }),
                DataTransferStatus.Offline => StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = result.Message }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = result.Message }),
            };
        }

        [HttpPost("Sync")]
        public async Task<IActionResult> Sync([FromQuery] int projectId, [FromQuery] bool release, CancellationToken cancellationToken)
        {
            DataTransferResult result;
            try
            {
                result = await _dataTransferService.SyncAsync(projectId, release, CurrentUsername ?? string.Empty, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sync {ProjectId} failed unexpectedly", projectId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Sync failed: " + ex.Message, uploaded = false });
            }

            return result.Status switch
            {
                DataTransferStatus.Ok => Ok(new { success = true, released = result.Released, message = result.Message }),
                DataTransferStatus.Offline => StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = result.Message }),
                DataTransferStatus.NotFound => NotFound(new { success = false, message = result.Message }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = result.Message, uploaded = result.Uploaded }),
            };
        }

        /// <summary>Release without sync: discards offline changes (reference DeleteSyncData + Release).</summary>
        [HttpPost("Release")]
        public async Task<IActionResult> Release([FromQuery] int projectId, CancellationToken cancellationToken)
        {
            DataTransferResult result;
            try
            {
                result = await _dataTransferService.ReleaseAsync(projectId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Release {ProjectId} failed unexpectedly", projectId);
                return StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = "Release failed: " + ex.Message });
            }

            return result.Status switch
            {
                DataTransferStatus.Ok => Ok(new { success = true }),
                DataTransferStatus.Offline => StatusCode(StatusCodes.Status503ServiceUnavailable, new { success = false, message = result.Message }),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new { success = false, message = result.Message }),
            };
        }

        /// <summary>Checks whether the project is currently downloaded according to the online server.</summary>
        [HttpGet("GetDownloadedData")]
        public async Task<IActionResult> GetDownloadedData([FromQuery] int projectId, [FromServices] IOnlineTransferRepository onlineRepository, CancellationToken cancellationToken)
        {
            try
            {
                var marker = await onlineRepository.GetDownloadedProjectAsync(projectId, cancellationToken);
                return Ok(marker);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetDownloadedData {ProjectId} failed to check online status", projectId);
                return StatusCode(StatusCodes.Status503ServiceUnavailable, "Online check failed");
            }
        }

        [HttpGet("GetDownloadedProjects")]
        public async Task<ActionResult<int[]>> GetDownloadedProjects(CancellationToken cancellationToken)
            => Ok((await _dataTransferService.GetDownloadedProjectsAsync(cancellationToken)).ToArray());

        [HttpGet("GetPendingChanges")]
        public async Task<IActionResult> GetPendingChanges([FromQuery] int projectId, CancellationToken cancellationToken)
        {
            var pending = await _dataTransferService.GetPendingChangesAsync(projectId, cancellationToken);
            return Ok(new { hasChanges = pending.HasChanges, sectionCount = pending.SectionCount });
        }

        /// <summary>
        /// Local/online state of the server "downloaded for offline" marker. Never fails: when the server cannot
        /// be read the lock fields report offline semantics.
        /// </summary>
        [HttpGet("GetOnlineLockStatus")]
        public async Task<IActionResult> GetOnlineLockStatus([FromQuery] int projectId, CancellationToken cancellationToken)
        {
            OnlineLockStatus status;
            try
            {
                status = await _dataTransferService.GetOnlineLockStatusAsync(projectId, CurrentUserObjectId, CurrentUsername, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetOnlineLockStatus {ProjectId} failed", projectId);
                status = new OnlineLockStatus();
            }

            return Ok(new
            {
                isLocal = status.IsLocal,
                isOnline = status.IsOnline,
                lockedByMe = status.LockedByMe,
                lockedBy = status.LockedBy == null ? null : new { name = status.LockedBy.Name, email = status.LockedBy.Email }
            });
        }

        /// <summary>
        /// Projects available to download for offline work (mirrors digital-cap-offline api/DataTransferAPI/GetProjectData).
        /// </summary>
        [HttpGet("GetProjectData")]
        public async Task<IActionResult> GetProjectData(CancellationToken cancellationToken)
        {
            var projects = await _dataTransferService.GetAvailableOnlineProjectsAsync(CurrentUserObjectId, CurrentUsername, cancellationToken);
            return Ok(projects);
        }
    }
}
