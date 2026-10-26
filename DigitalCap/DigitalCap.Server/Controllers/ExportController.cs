using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models.ExportConfig;
using DigitalCap.Core.Models.ImageDescription;
using DigitalCap.Core.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;


namespace DigitalCap.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ExportController : ApiBaseController
    {
        private readonly IExportService _exportService;
        private readonly ISecurityService _securityService;
        public ExportController(IExportService exportService, ISecurityService securityService)
        {
            _exportService = exportService;
            _securityService = securityService;
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> ManageExport()
        {
            var result = await _exportService.GetExportSettingsAsync();

            if (!result.IsSuccess)
                return BadRequest(result.Message);

            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetReportExportPartByVesselType(int vesselTypeId)
        {
            var result = await _exportService.GetExportPartsByVesselTypeAsync(vesselTypeId);

            if (!result.IsSuccess)
                return BadRequest(result);

            return Ok(result.Data.exportParts);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetReportExportPartByImo(int projectId)
        {
            var result = await _exportService.GetExportSettingsByProjectAsync(projectId);

            if (!result.IsSuccess)
                return BadRequest(result.Message);

            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> SaveRequiredInExport([FromBody] ExportSettingsRequest settings)
        {
            var user = await _securityService.GetCurrentUserAsync();
            var username = user.Data.UserName;

            var result = await _exportService.SaveRequiredInExportAsync(settings, username);

            if (!result.IsSuccess)
                return BadRequest(result.Message);

            return Ok(true);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetProjectListByIMO([FromQuery] int? imoNumber)
        {
            var result = await _exportService.GetProjectListByIMOAsync(imoNumber);

            if (!result.IsSuccess)
                return BadRequest(result.Message);

            return Ok(result.Data);
        }

    }
}
