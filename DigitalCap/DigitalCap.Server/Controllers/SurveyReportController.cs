using DigitalCap.Core.Enumerations;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.ReportConfig;
using DigitalCap.Core.Models.ExportConfig;
using DigitalCap.Core.Models.Skylight;
using DigitalCap.Core.Models.Survey;
using DigitalCap.Core.Models.Tank;
using DigitalCap.Core.Security;
using DigitalCap.Core.ViewModels;
using DigitalCap.Infrastructure.Service;
using DigitalCap.WebApi.Auth;
using DigitalCap.WebApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace DigitalCap.WebApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class SurveyReportController : ControllerBase
    {
        private readonly ISurveyReportService _surveyReportService;
        private readonly IConfiguration _configData;
        private readonly IConfiguration _configuration;
        private readonly IAsposePdfService _asposePdfService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        public SurveyReportController(ISurveyReportService surveyReportService, IConfiguration configData, IConfiguration configuration, IAsposePdfService asposePdfService, IWebHostEnvironment webHostEnvironment)
        {
            _surveyReportService = surveyReportService;
            _configData = configData;
            _configuration = configuration;
            _asposePdfService = asposePdfService;
            _webHostEnvironment = webHostEnvironment;
        }
        [HttpGet("[action]")]
        public async Task<IActionResult> SurveyReportIndex(int projectId, int? templateSectionId = null)
        {
            
            // The offline identity's Name claim is the display name; its permissions are stored under the e-mail.
            var currentUserName = User.FindFirstValue(ClaimTypes.Upn)
                ?? (User.Identity?.AuthenticationType == OfflineAuthenticationHandler.SchemeName ? User.FindFirstValue(ClaimTypes.Email) : null)
                ?? User.FindFirstValue(ClaimTypes.Name);

            var result = await _surveyReportService.GetIndexAsync(projectId, currentUserName, templateSectionId);

            if (!result.IsSuccess && result.ErrorMessage == "REDIRECT_TO_SURVEY")
            {
                return RedirectToAction("Index", "Survey", new { projectId = projectId });
            }

            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage, result.ErrorMessages, result.ErrorCode });
            return Ok(result.Data);
        }

        //public IActionResult ConfigSurveyReport(SurveyReportViewModel model)
        //{
        //    return View(model);

        //}
        [HttpGet("[action]")]
        public async Task<IActionResult> IndexPartial(int projectId, int? templateSectionId = null)
        {
            var result = await _surveyReportService.IndexPartial(projectId, templateSectionId);

            return Ok(result.Data);
        }

        //public async Task<IActionResult> HelpPartial()
        //{
        //    return PartialView("_VesselEvaluationMethodAndGradingSystem");
        //}
        [HttpGet("[action]")]
        public async Task<IActionResult> GetBulkUploadImagesList()
        {
            var result = await _surveyReportService.GetBulkUploadImagesList();
            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetIsElectron()
        {
            var isElectron = Convert.ToBoolean(_configData["IsElectron"]);
            var defaultProjectId = _configuration.GetValue<int>("DefaultTemplateProjectId");
            return Ok(new
            {
                isElectron = isElectron,
                defaultProjectId = defaultProjectId
            });
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetBulkUploadSectionDropdown(int projectId)
        {
            var result = await _surveyReportService.GetBulkUploadSectionDropdown(projectId);

            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> CreateReport(int projectId, int templateSectionId)
        {
            var result = await _surveyReportService.CreateReport(projectId, templateSectionId);

            return Ok(result.Data);
        }
        [HttpPost("[action]")]
        public async Task<IActionResult> DeleteReport(int projectId, int templateSectionId)
        {
            var result = await _surveyReportService.DeleteReport(projectId, templateSectionId);

            return Ok(result);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> SurveyReportPDF([FromBody] ExportReportDto exportReportDto)
        {
            if (!exportReportDto.TemplateId.HasValue || exportReportDto.TemplateId == 0)
                exportReportDto.SectionId = Guid.NewGuid().ToString();

            var result = await _surveyReportService.SurveyReportPDF(
                exportReportDto.ProjectId,
                exportReportDto.TemplateId,
                exportReportDto.SectionId,
                exportReportDto.AllTemplate
            );

            if (!result.IsSuccess)
                return BadRequest(result.ErrorMessage);

            //var html = SurveyReportHtmlBuilder.Build(result.Data);

            //var pdf = _pdfService.GeneratePdf(html);

            //var fileName = $"{result.Data.ProjectName}_{exportReportDto.ProjectId}_{DateTime.Now:dd_MM_yyyy_HH_mm_ss}.pdf";

            return Ok(result.Data);
        }
        [HttpPost("[action]")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(ValueLengthLimit = 268435456)]
        public async Task<ActionResult> SaveReportFile(string contentType, string base64, bool allTemplate, string id, string sectionId, int totalTemplates, int totalSections, int templateId, int projectId)
        {
            try
            {
                var fileContents = Convert.FromBase64String(base64);
                // Validate the id parameter
                if (id.Contains("..") || id.Contains("/") || id.Contains("\\"))
                {
                    return BadRequest("Invalid path");
                }

                var result = await _surveyReportService.SaveReportFile(contentType, base64, allTemplate, id, sectionId, totalTemplates, totalSections, templateId, projectId, fileContents);

                return Content(JsonConvert.SerializeObject(result.Data));
            }
            catch (Exception ex)
            {
                return Content(JsonConvert.SerializeObject(null));
            }
        }

        [HttpGet("[action]")]
        public async Task<ActionResult> Export(int projectId, bool allTemplate, int templateId)
        {
            var result = await _surveyReportService.Export(projectId, allTemplate, templateId);

            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> MergeReportPDF(string id, bool allTemplate, int projectId, int templateId)
        {
            var result = await _surveyReportService.MergeReportPDF(id, allTemplate, projectId, templateId);


            return Content(JsonConvert.SerializeObject(result.Data));
        }
        [HttpGet("[action]")]
        public int GetGradingCount(List<GradingUI> gradings, Guid sectionId)
        {
            try
            {
                var count = gradings.Where(x => x.SectionId == sectionId).ToList().Count;
                return count;
            }
            catch { }
            return 0;
        }
        [HttpGet("[action]")]
        public async Task<int> GetUploadedImageCount(int projectId, int templateId, Guid sectionId)
        {
            var result = await _surveyReportService.GetUploadedImageCount(projectId, templateId, sectionId);

            return result;
        }
        [HttpGet("[action]")]
        public async Task<IActionResult> GetPhotoPlacementDropdownPartial(int projectId, int templateId, Guid sectionId)
        {
            //var sectionIds = GetSectionIdsByProjectId(projectId, templateId).Result.ReportPartGrid.Where(x => x.TemplateId == templateId).FirstOrDefault().SectionIds;
            //SurveyPhotoPlacementDropdownsViewModel surveyPhotoPlacementDropdownViewModel = _surveyReportRepository.GetBulkUploadDropdownValues(projectId, templateId, sectionId, sectionIds);
            //return PartialView("_SurveyPhotoPlacementDropdownsPartial", surveyPhotoPlacementDropdownViewModel);

            var result = await _surveyReportService.GetPhotoPlacementDropdownPartial(projectId, templateId, sectionId);

            return Ok(result.Data);

        }
        //[HttpGet("[action]")]
        //public PhotoPlacementSequence GetPhotoCardsInList(List<Template> allCards, List<UpSkillCard> sequenceCards, List<string> populatedCardIds, string type)
        //{

        //    var placementSequence = _surveyReportService.GetPhotoCardsInList(allCards, sequenceCards, populatedCardIds, type);

        //    return placementSequence;
        //}
        [HttpPost("[action]")]
        public async Task<IActionResult> UpdatePhotoUploadPartial(int projectId, string assignmentId)
        {
            var count = await _surveyReportService.UpdatePhotoUploadPartial(projectId, assignmentId);

            return Ok(count);
        }
        [HttpGet("[action]")]
        public async Task<IActionResult> GetSurveyPhotoPlacementPartial(int projectId, int templateSectionId, string assignmentId, string appIds)
        {
            var result = await _surveyReportService.GetSurveyPhotoPlacementPartial(projectId, templateSectionId, assignmentId, appIds);

            return Ok(result.Data);
        }
        [HttpGet("[action]")]
        public async Task<IActionResult> DownloadImageForPlacement(int imageId)
        {
            var result = await _surveyReportService.DownloadImageForPlacement(imageId);
            return Ok(result.Data);
        }
        [HttpGet("[action]")]
        public async Task<IActionResult> GetUserApplications()
        {
            var final = await _surveyReportService.GetUserApplications();
            return Ok(final);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> RefreshSurveyStatus(int projectId)
        {
            var result = await _surveyReportService.RefreshCertificate(projectId);

            return Ok(result.Data);
        }
        [HttpPost("[action]")]
        public async Task<IActionResult> RefreshCertificates(int projectId)
        {
            var result = await _surveyReportService.RefreshCertificate(projectId);
            return Ok(result.Data);
        }
        [HttpGet("[action]")]
        public async Task<IActionResult> GetReport(int projectId, int templateSectionId)
        {
            try
            {
                List<string> sectionIds = new List<string>();
                try
                {
                    var sectionResult = await _surveyReportService
                        .GetSectionIdsByProjectId(projectId, templateSectionId);

                    if (sectionResult.IsSuccess && sectionResult.Data != null)
                    {
                        var reportPart = sectionResult.Data.ReportPartGrid
                            .FirstOrDefault(x =>
                                x.TemplateId == templateSectionId
                            //||
                            //x.PartId == templateSectionId
                            );

                        sectionIds = reportPart?.SectionIds?
                            .Select(x => x.ToString())
                            .ToList()
                            ?? new List<string>();
                    }
                }
                catch
                {
                    // sectionIds empty — GetReport still works
                }

                var reportResult = await _surveyReportService.GetReport(projectId, templateSectionId, sectionIds);

                return Ok(reportResult);
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Internal server error");
            }
        }
        [HttpPost("[action]")]
        public async Task<IActionResult> GetReportBySectionId(int projectId, int templateSectionId, Guid sectionId)
        {
            var result = await _surveyReportService.GetReportBySectionId(projectId, templateSectionId, sectionId);
            if (!result.IsSuccess)
                return BadRequest(new { result.ErrorMessage, result.ErrorMessages });
            return Ok(result.Data);
        }


        [HttpPost("[action]")]
        public async Task<IActionResult> ValidateReport(int projectId, int templateSectionId)
        {
            var result = await _surveyReportService.ValidateReport(projectId, templateSectionId);

            return Ok(result);
        }

        // [HttpPost("[action]")]
        //public ActionResult ValidatePreviousTabReport(int projectId)
        //{
        //    try
        //    {
        //        var resultsession = "";
        //        #region project id checking
        //        if (HttpContext.Session.GetInt32("projectid") != null)
        //        {
        //            int projId = (int)HttpContext.Session.GetInt32("projectid");
        //            #region Session
        //            if (projId == projectId)
        //            {
        //                if (HttpContext.Session.GetString("reportsession") != null)
        //                {
        //                    resultsession = HttpContext.Session.GetString("reportsession");
        //                }
        //            }

        //            #endregion session

        //        }
        //        return Json(resultsession);
        //        #endregion
        //    }
        //    catch (Exception ex)
        //    {
        //        return Json(ex.Message);
        //    }

        //}

        [HttpPost("[action]")]
        public async Task<IActionResult> ValidateGrading([FromBody] ValidateGradingDto dto)
        {
            var result = await _surveyReportService.ValidateGrading(
                dto.ProjectId,
                dto.TemplateSectionId
            );

            return Ok(result.Data);
        }
        [HttpPost("[action]")]
        public async Task<IActionResult> ExportAllValidation(int projectId)
        {
            var result = await _surveyReportService.ExportAllValidation(projectId);
            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> ExportValidationPartial(int projectId, int sectionid)
        {
            var result = await _surveyReportService.ExportValidationPartial(projectId, sectionid);
            return Ok(result.Data);
        }

        // [AllowAnonymous]
        //[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        //public IActionResult Error()
        //{
        //    return Ok(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        //}

        [NonAction]
        public IActionResult Error()
        {
            return Problem("An error occurred");
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> DeleteImage(string assignmentId, string fileId)
        {
            var ret = await _surveyReportService.DeleteImage(assignmentId, fileId);
            return Ok(ret);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> DeleteCard(int projectId, string cardId, string sequenceId, string assignmentId, string applicationId)
        {
            var ret = await _surveyReportService.DeleteCard(projectId, cardId, sequenceId, assignmentId, applicationId);
            return Ok(ret);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> UploadPhoto(IFormFile file, [FromForm] int projectId, string cardId, string sequenceId, string assignmentId, string applicationId, string label, string appName, string fileId, bool replaceImage = false)
        {
            var result = await _surveyReportService.UploadPhoto(file, projectId, cardId, sequenceId, assignmentId, applicationId, label, appName, fileId, replaceImage);

            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public IActionResult Delete(int? fileId)
        {
            if (!fileId.HasValue)
                return NotFound();

            var result = _surveyReportService.DeleteBulkUploadImage(fileId.Value);
            return Ok(result);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> UploadUnPlacedPhoto(IFormFile uploadedFile, FileDataType dataType, int projectId, int taskId, string assignmentId)
        {
            string newfileId = Guid.NewGuid().ToString();
            var result = await _surveyReportService.UploadUnPlacedPhoto(uploadedFile, dataType, projectId, taskId, assignmentId);

            return Ok(result.Data);
        }


        [HttpPost("[action]")]
        public async Task<ActionResult> UpdateGenericImageDescriptionDropdownCard([FromForm] int templateId, [FromForm] int projectId, [FromForm] Guid sectionId, [FromForm] int cardId, [FromForm] int value, [FromForm] string cardName)
        {
            var result = await _surveyReportService.UpdateGenericImageDescriptionDropdownCard(templateId, projectId, sectionId, cardId, value, cardName);

            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> UpdateGenericAdditionalDescription([FromForm] int templateId, [FromForm] int projectId, [FromForm] Guid sectionId, [FromForm] int cardId, [FromForm] string value, [FromForm] string cardName)
        {
            var result = await _surveyReportService.UpdateGenericAdditionalDescription(templateId, projectId, sectionId, cardId, value, cardName);
            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> UpdateGenericCurrentCondition([FromForm] int templateId, [FromForm] int projectId, [FromForm] Guid sectionId, [FromForm] int cardId, [FromForm] int value, [FromForm] string cardName)
        {
            var result = await _surveyReportService.UpdateGenericCurrentCondition(templateId, projectId, sectionId, cardId, value, cardName);
            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> UploadGenericImage(IFormFile file, [FromForm]  int templateId, [FromForm]  int projectId, [FromForm]  Guid sectionId, [FromForm]  int cardId, [FromForm]  int imageId, [FromForm]  bool replaceImage = false, [FromForm]  string cardName = null)
        {
            var result = await _surveyReportService.UploadGenericImage(file, templateId, projectId, sectionId, cardId, imageId, replaceImage, cardName);

            //  return Json(new { FileId = newfileId, Image = resizedImage }, new JsonSerializerOptions { PropertyNamingPolicy = null });
            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> DeleteGenericImage(int templateId, int projectId, Guid sectionId, int cardId, int imageId)
        {
            var result = await _surveyReportService.DeleteGenericImage(templateId, projectId, sectionId, cardId, imageId);

            return Ok(result);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> UploadImage([FromForm] UploadImageDto dto)
        {
            var result = await _surveyReportService.UploadImage(
                dto.File,
                dto.TemplateId,
                dto.ProjectId,
                dto.SectionId,
                dto.CardId,
                dto.ImageId,
                dto.ReplaceImage
            );

            if (result.IsSuccess)
                return Ok(result.Data);

            return BadRequest(result.ErrorMessage);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> DeleteProjectImage([FromQuery] int templateId, [FromQuery] int projectId, [FromQuery] Guid sectionId, [FromQuery] int cardId, [FromQuery] int imageId)
        {
            var result = await _surveyReportService.DeleteImage(
                templateId,
                projectId,
                sectionId,
                cardId,
                imageId);

            return Ok(result);
        }
        [HttpPost("[action]")]
        public async Task<IActionResult> UpdateImageDescriptionDropdownCard([FromBody] UpdateImageDescriptionDto dto)
        {
            var result = await _surveyReportService.UpdateImageDescriptionDropdownCard(dto.TemplateId, dto.ProjectId, dto.SectionId, dto.CardId, dto.Value);
            return Ok(result);
        }
        [HttpPost("[action]")]
        public async Task<IActionResult> UpdateAdditionalDescription([FromBody] UpdateAdditionalDescriptionDto dto)
        {
            var result = await _surveyReportService.UpdateAdditionalDescription(dto.TemplateId, dto.ProjectId, dto.SectionId, dto.CardId, dto.Value);

            return Ok(result);
        }
        [HttpPost("[action]")]
        public async Task<IActionResult> UpdateCurrentCondition([FromBody] UpdateCurrentConditionDto dto)
        {
            var result = await _surveyReportService.UpdateCurrentCondition(
                dto.TemplateId,
                dto.ProjectId,
                dto.SectionId,
                dto.CardId,
                dto.Value
            );

            return Ok(result);
        }
        //New Image Card Funcions

        [HttpPost("[action]")]
        public async Task<IActionResult> UpdateSectionNote(int templateId, int projectId, Guid sectionId, string value)
        {
            var result = await _surveyReportService.UpdateSectionNote(templateId, projectId, sectionId, value);
            return Ok(result);
        }


        [HttpPost("[action]")]
        public async Task<IActionResult> UpdateGradingNotes([FromBody] UpdateNotesDto dto)
        {
            var result = await _surveyReportService.UpdateGradingNotes(
                dto.TemplateId,
                dto.ProjectId,
                dto.SectionId,
                dto.GradingId,
                dto.Value
            );

            return Ok(result);
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> UpdateGradingCondition([FromBody] UpdateGradingDto dto)
        {
            var result = await _surveyReportService.UpdateGradingCondition(
                dto.TemplateId,
                dto.ProjectId,
                dto.SectionId,
                dto.GradingId,
                dto.Choices
            );

            return Ok(result);
        }
        ///
        [HttpPost("[action]")]
        public async Task<IActionResult> UpdateH_IReport([FromBody] UpdateHIReportDto request)
        {
            var result = await _surveyReportService.UpdateH_IReport(
                request.Id,
                request.ProjectId,
                request.CompanyName,
                request.CertificateNumber,
                request.ExpiryDate ?? DateTime.MinValue,
                request.ThicknessMeasurementA,
                request.ThicknessMeasurementB,
                request.ThicknessMeasurementC,
                request.AdditionalSurveyorComments,
                request.MaintanceExecuted,
                request.Accommodation,
                request.PhotoGraphs,
                request.OperationalTestLocation,
                request.OperationalTestStart ?? DateTime.MinValue,
                request.OperationalTestFinish ?? DateTime.MinValue,
                request.OptionalTestSatisFactory,
                request.AssociatedReportNumber,
                request.AssociatedReportDate,
                request.Remarks,
                request.LeadSurveyorName,
                request.LeadSurveyorNumber,
                request.SurveyorName,
                request.SurveyorNumber,
                request.LastVisitDate ?? DateTime.MinValue,
                request.IndividualA,
                request.IndividualB,
                request.IndividualC,
                request.IndividualD,
                request.IndividualE,
                request.IndividualF,
                request.ScouringOrPitting,
                request.SubstantialCorrosionRemaining,
                request.StatutoryDeficiencies,
                request.FoundSatisfactory,
                request.UnderRemarks,
                request.BottomPlattingScouringOrPitting);

            return Ok(result);
        }
        [HttpPost("[action]")]
        public async Task<ActionResult> UpdateLayoutInput(Report report, int projectId, string cardId, string sequenceId, string assignmentId, string applicationId, string appName, string inputValue, string inputLabel = "")
        {
            var result = await _surveyReportService.UpdateLayoutInput(report, projectId, cardId, sequenceId, assignmentId, applicationId, appName, inputValue, inputLabel);
            return Ok(result);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> UpdateReport([FromBody] UpdateReportRequest request)
        {
            var result = await _surveyReportService.UpdateReport(
                request.ProjectId,
                request.TemplateId,
                request.ReportPart,
                request.Value,
                request.Imonumber,
                request.HullNo,
                request.SurveyDate
            );

            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> PlaceImage(int projectId, int templateId, Guid sectionId, int imageId, int cardId, string cardName, int descriptionId, int currentConditionId)
        {
            var result = await _surveyReportService.PlaceImage(projectId, templateId, sectionId, imageId, cardId, cardName, descriptionId, currentConditionId);
            return Ok(result.Data);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> UpdateDropdownSelection(int projectId, string cardId, string sequenceId, string assignmentId, string applicationId, string appName, string label, string value)
        {
            var result = await _surveyReportService.UpdateDropdownSelection(projectId, cardId, sequenceId, assignmentId, applicationId, appName, label, value);
            return Ok(result);
        }

        [HttpPost("[action]")]
        public async Task<ActionResult> UpdateGradingSelection(int projectId, string cardId, string sequenceId, string assignmentId, string applicationId, string appName, string label, string selectedKeys = "")
        {
            var result = await _surveyReportService.UpdateGradingSelection(projectId, cardId, sequenceId, assignmentId, applicationId, appName, label, selectedKeys);
            return Ok(result);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> GetPhotoPlacementDropdownData(int projectId, int templateId, Guid sectionId)
        {
            var result = await _surveyReportService.GetPhotoPlacementDropdownData(projectId, templateId, sectionId);

            if (!result.IsSuccess)
                return BadRequest(result.Message);

            return Ok(result.Data);
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> ExportCAPReportPdf(int projectId)
        {
            var (pdfBytes, fileName) = await _asposePdfService.GenerateCAPReportPdf(projectId);

            if (pdfBytes == null || pdfBytes.Length == 0)
                return BadRequest("Failed to generate PDF");

            return File(pdfBytes, "application/pdf", $"{fileName}.pdf");
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> ExportPartPdf(int projectId, int templateId, [FromBody] List<ExportPart> selectedSettings = null)
        {
            var (pdfBytes, fileName) = await _asposePdfService.GeneratePartReportPdf(projectId, templateId, selectedSettings);

            if (pdfBytes == null || pdfBytes.Length == 0)
                return BadRequest("Failed to generate PDF");

            return File(pdfBytes, "application/pdf", $"{fileName}_Part.pdf");
        }

        [HttpGet("[action]")]
        public async Task<IActionResult> ExportHIPdf(int projectId, int templateId)
        {
            var (pdfBytes, fileName) = await _asposePdfService.GenerateHIReportPdf(projectId, templateId);

            if (pdfBytes == null || pdfBytes.Length == 0)
                return BadRequest("Failed to generate H&I PDF");

            return File(pdfBytes, "application/pdf", $"{fileName}.pdf");
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> ExportFullPdf(int projectId, [FromBody] List<ExportPart> selectedSettings = null, int? templateSectionId = null)
        {
            //var currentUserName = User.Identity?.Name;
            var currentUserName = User.FindFirstValue(ClaimTypes.Upn)
              ?? User.FindFirstValue(ClaimTypes.Name);

            var (pdfBytes, fileName) = await _asposePdfService.GenerateFullReportPdf(projectId, currentUserName, templateSectionId, selectedSettings);

            if (pdfBytes == null || pdfBytes.Length == 0)
                return BadRequest("Failed to generate full PDF");

            return File(
                pdfBytes,
                "application/pdf",
                $"{fileName}.pdf"
            );
        }
        [HttpGet("[action]")]
        public IActionResult GetConfig()
        {
            return Ok(new
            {
                defaultTemplateProjectId = _configuration.GetValue<int>("DefaultTemplateProjectId")
            });
        }
    }
}

