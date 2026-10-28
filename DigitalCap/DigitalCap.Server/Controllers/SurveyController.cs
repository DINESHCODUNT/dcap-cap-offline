using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.ExportConfig;
using DigitalCap.WebApi.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Security.Claims;

namespace DigitalCap.WebApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SurveyController : ControllerBase
    {
        private readonly IProjectSurveyService _projectSurveyService;

        public SurveyController(IProjectSurveyService projectSurveyService)
        {
            _projectSurveyService = projectSurveyService;
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> Index(int projectId, int? templateSectionId = null)
        {
            var user = User.Identity?.Name;

            // The offline identity's Name claim is the display name; its permissions are stored under the e-mail.
            var currentUserName = User.FindFirstValue(ClaimTypes.Upn)
               ?? (User.Identity?.AuthenticationType == OfflineAuthenticationHandler.SchemeName ? User.FindFirstValue(ClaimTypes.Email) : null)
               ?? User.FindFirstValue(ClaimTypes.Name);

            var result = await _projectSurveyService.GetSurveyIndexAsync(projectId, currentUserName, templateSectionId);
            if (!result.IsSuccess)
                return StatusCode(403, new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> IndexPartial(int projectId, int? templateSectionId = null, int imo = 0)
        {
            var result = await _projectSurveyService.IndexPartialAsync(projectId, templateSectionId, imo);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> CheckShowExportAllSettings(int projectId, int imo = 0)
        {
            var result = await _projectSurveyService.CheckShowExportAllSettingsAsync(projectId, imo);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> CheckShowExportSettings(int projectId, int? templateId = null, int imo = 0)
        {
            if (templateId == null)
                return BadRequest("templateId is required");
            var result = await _projectSurveyService.CheckShowExportSettingsAsync(projectId, imo, templateId.Value);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetProjectExportConfig(int projectId, int? templateId = null, int imo = 0)
        {
            var result = await _projectSurveyService.PrepareProjectExportSettingsAsync(projectId, templateId, imo);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetAllSections(int projectId, int? templateId = null)
        {
            if (templateId == null)
                return BadRequest("templateId is required");
            var result = await _projectSurveyService.GetAllSectionsAsync(projectId, templateId.Value);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetAllParts(int projectId, int imo = 0)
        {
            var result = await _projectSurveyService.GetAllPartsAsync(projectId, imo);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetProjectExportAllConfig(int projectId, int imo = 0)
        {
            var result = await _projectSurveyService.PrepareProjectExportSettingsAsync(projectId, null, imo);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> CreateProjectReport(int projectId, int templateSectionId)
        {
            var result = await _projectSurveyService.CreateProjectReportAsync(projectId, templateSectionId);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetProjectReport(int projectId, int partId, int imo)
        {
            var result = await _projectSurveyService.GetProjectReportAsync(projectId, partId, imo);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetProjectReportBySection(int projectId, int partId, Guid sectionId, int imo)
        {
            var result = await _projectSurveyService.GetProjectReportBySectionIdAsync(projectId, partId, sectionId, imo);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> Export(int projectId, bool allTemplate, int templateId, int imo, [FromQuery] string? settingsJson = null)
        {
            List<ExportPart>? settings = null;
            if (!string.IsNullOrEmpty(settingsJson))
                settings = System.Text.Json.JsonSerializer.Deserialize<List<ExportPart>>(settingsJson);

            var result = await _projectSurveyService.GetProjectExportViewModelAsync(projectId, allTemplate, templateId, imo, settings);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        /// <summary>MVC stored export parts in session; this API is stateless — return the payload so the client can pass <see cref="ExportPart"/> to <see cref="MergeReportPdf"/>.</summary>
        [HttpPost("[action]")]
        public IActionResult ExportAllConfig([FromQuery] int projectId, [FromQuery] bool allTemplate, [FromQuery] int templateId, [FromQuery] int imo, [FromBody] ExportRequest? request)
        {
            return Ok(new { status = "success", data = request?.Data });
        }

        [HttpPost("[action]")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(ValueLengthLimit = 268435456)]
        public async Task<IActionResult> SaveReportFile([FromQuery] string? contentType, [FromQuery] string? base64, [FromQuery] bool allTemplate, [FromQuery] string? id, [FromQuery] string? sectionId, [FromQuery] int totalTemplates, [FromQuery] int totalSections, [FromQuery] int templateId, [FromQuery] int projectId, [FromBody] byte[]? fileContents)
        {
            if (fileContents == null || fileContents.Length == 0)
                return BadRequest("file body required");
            var result = await _projectSurveyService.SaveProjectReportFileAsync(id ?? "", sectionId ?? "", totalSections, templateId, projectId, fileContents);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(new { allTemplatesCompleted = result.Data });
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> MergeReportPdf([FromQuery] string id, [FromQuery] bool allTemplate, [FromQuery] int projectId, [FromQuery] int templateId, [FromQuery] int imo, [FromBody] List<ExportPart>? settings)
        {
            var result = await _projectSurveyService.MergeProjectSurveyPdfAsync(id, allTemplate, projectId, templateId, imo, settings);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Content(Newtonsoft.Json.JsonConvert.SerializeObject(result.Data), "application/json");
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetBulkUploadImagesList()
        {
            var result = await _projectSurveyService.GetBulkUploadImagesListAsync();
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetProjectBulkUploadSectionDropdown(int projectId, int imo = 0)
        {
            var result = await _projectSurveyService.GetProjectBulkUploadSectionDropdownAsync(projectId, imo);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetProjectSurveyPhotoPlacementPartial(int projectId, int templateSectionId)
        {
            var result = await _projectSurveyService.GetProjectSurveyPhotoPlacementPartialAsync(projectId, templateSectionId);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetPhotoPlacementDropdownPartial(int projectId, int templateId, Guid sectionId, int imo)
        {
            var result = await _projectSurveyService.GetPhotoPlacementDropdownPartialAsync(projectId, templateId, sectionId, imo);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> PlaceImage([FromQuery] int projectId, [FromQuery] int templateId, [FromQuery] Guid sectionId, [FromQuery] int imageId, [FromQuery] int cardId, [FromQuery] string cardName, [FromQuery] int descriptionId, [FromQuery] int currentConditionId)
        {
            var result = await _projectSurveyService.PlaceImageAsync(projectId, templateId, sectionId, imageId, cardId, cardName, descriptionId, currentConditionId);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> SurveyPdf(int projectId, int? templateId, string? sectionId, bool allTemplate, int imo, int order, bool displayTitle)
        {
            var result = await _projectSurveyService.GetSurveyPdfModelAsync(projectId, templateId, sectionId, imo, order, displayTitle);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> ValidateProjectReport([FromQuery] int projectId, [FromQuery] int templateSectionId, [FromQuery] int imo, [FromQuery] string? reportSessionState = null)
        {
            var result = await _projectSurveyService.ValidateProjectReportAsync(projectId, templateSectionId, imo, reportSessionState);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Content(JsonConvert.SerializeObject(result.Data), "application/json");
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> ValidateProjectPreviousTabReport([FromQuery] string? reportSessionState = null)
        {
            var result = await _projectSurveyService.ValidateProjectPreviousTabReportAsync(reportSessionState);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Content(JsonConvert.SerializeObject(result.Data), "application/json");
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> ValidateProjectGrading([FromQuery] int projectId, [FromQuery] int templateSectionId, [FromQuery] int imo, [FromBody] List<ExportSections>? settings)
        {
            var result = await _projectSurveyService.ValidateProjectGradingAsync(projectId, templateSectionId, imo, settings);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Content(JsonConvert.SerializeObject(result.Data), "application/json");
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> ExportAllProjectValidation([FromQuery] int projectId, [FromQuery] int imo, [FromBody] List<ExportPart> settings)
        {
            var result = await _projectSurveyService.ExportAllProjectValidationAsync(projectId, imo, settings ?? new List<ExportPart>());
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Content(JsonConvert.SerializeObject(result.Data), "application/json");
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> ExportAllProjectPartialValidation([FromQuery] int projectId, [FromQuery] int imo, [FromQuery] int partid, [FromBody] List<ExportPart> settings)
        {
            var result = await _projectSurveyService.ExportAllProjectPartialValidationAsync(projectId, imo, settings ?? new List<ExportPart>(), partid);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Content(JsonConvert.SerializeObject(result.Data), "application/json");
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> UpdateHandIGradingNotes([FromQuery] int projectId, [FromQuery] int gradingId, [FromQuery] string value)
        {
            var result = await _projectSurveyService.UpdateHandIGradingNotesAsync(projectId, gradingId, value);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> UpdateHandIGradingCondition([FromQuery] int projectId, [FromQuery] int gradingId, [FromQuery] string choiceName, [FromQuery] bool value)
        {
            var result = await _projectSurveyService.UpdateHandIGradingConditionAsync(projectId, gradingId, choiceName, value);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage });
            return Ok(result.Data);
        }
    }
}
