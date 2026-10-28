using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Services;
using ElectronNET.API;
using DigitalCap.Infrastructure.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalCap.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class OfflineController : ControllerBase
    {
        private readonly OnlineStatusService _onlineStatusService;
        private readonly OneDrivePathService _pathService;
        private readonly ISyncRepository _syncRepository;

        public OfflineController(OnlineStatusService onlineStatusService, ISyncRepository syncRepository)
        {
            _onlineStatusService = onlineStatusService;
            _syncRepository = syncRepository;
            _pathService = new OneDrivePathService();
        }

        /// <summary>
        /// The user the desktop app works as while offline (recorded at their last online sign-in).
        /// Only answers inside the Electron app and to local callers.
        /// </summary>
        [HttpGet("[action]")]
        public async Task<IActionResult> GetOfflineUser()
        {
            var remote = HttpContext.Connection.RemoteIpAddress;
            if (!HybridSupport.IsElectronActive || (remote != null && !IPAddress.IsLoopback(remote)))
                return NotFound();

            var user = await _syncRepository.GetUserInfo();
            if (user == null || string.IsNullOrWhiteSpace(user.EmailId))
                return NotFound();

            return Ok(new
            {
                id = user.Id,
                emailId = user.EmailId,
                firstName = user.FirstName,
                lastName = user.LastName
            });
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetOnlineStatus()
        {
            bool online = await _onlineStatusService.DetermineOnlineStatus();
            return Ok(new
            {
                isOnline = online,
                mode = online ? "Online" : "Offline"
            });
        }

        [HttpGet("[action]")]
        public IActionResult GetOnlineUrl([FromServices] Microsoft.Extensions.Configuration.IConfiguration config)
        {
            return Ok(new { url = config["URL"] });
        }

        [HttpGet("[action]")]
        public IActionResult CheckDownloadedProjects([FromServices] Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            var workingDb = _pathService.GetDatabasePath();
            bool hasProjects = System.IO.File.Exists(workingDb);

            return Ok(new
            {
                hasDownloadedProjects = hasProjects,
                databasePath = env.IsDevelopment() ? workingDb : null
            });
        }

        [HttpPost("[action]")]
        [HttpGet("[action]")]
        public async Task<bool> UpdateReleasedStatus([FromQuery] int projectId, [FromServices] IProjectRepository projectRepository)
        {
            return await projectRepository.UpdateReleasedData(projectId);
        }

        [HttpGet("[action]")]
        [HttpPost("[action]")]
        public IActionResult DownloadDB([FromServices] Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            var remote = HttpContext.Connection.RemoteIpAddress;
            if (!HybridSupport.IsElectronActive || (remote != null && !IPAddress.IsLoopback(remote)))
                return NotFound();

            var dbFilePath = _pathService.GetDatabasePath();
            if (!System.IO.File.Exists(dbFilePath))
            {
                var fallback = Path.Combine(env.WebRootPath ?? env.ContentRootPath, AppKeyService.EncryptedDb);
                if (System.IO.File.Exists(fallback))
                {
                    dbFilePath = fallback;
                }
                else
                {
                    return NotFound("Database file not found");
                }
            }

            var downloadDir = _pathService.GetDownloadDirectory();
            if (!Directory.Exists(downloadDir))
            {
                Directory.CreateDirectory(downloadDir);
            }
            var downloadFilePath = Path.Combine(downloadDir, AppKeyService.EncryptedDb);
            System.IO.File.Copy(dbFilePath, downloadFilePath, true);

            var fileBytes = System.IO.File.ReadAllBytes(downloadFilePath);
            return File(fileBytes, "application/octet-stream", "sqldb-cap-dev-001.db.enc");
        }
    }
}
