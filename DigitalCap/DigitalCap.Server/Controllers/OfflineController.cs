using System;
using System.IO;
using System.Threading.Tasks;
using DigitalCap.Core.Services;
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

        public OfflineController(OnlineStatusService onlineStatusService)
        {
            _onlineStatusService = onlineStatusService;
            _pathService = new OneDrivePathService();
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
        public IActionResult CheckDownloadedProjects()
        {
            var workingDb = _pathService.GetDatabasePath();
            bool hasProjects = System.IO.File.Exists(workingDb);

            return Ok(new
            {
                hasDownloadedProjects = hasProjects,
                databasePath = workingDb
            });
        }
    }
}
