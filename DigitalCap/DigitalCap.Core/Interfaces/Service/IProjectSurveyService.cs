using DigitalCap.Core.Models;
using DigitalCap.Core.Models.ExportConfig;
using DigitalCap.Core.Models.ReportConfig;
using DigitalCap.Core.ViewModels;
using System.Collections.Generic;

namespace DigitalCap.Core.Interfaces.Service
{
    /// <summary>
    /// Survey (project-template) flow migrated from MVC <c>SurveyController</c> — uses <see cref="ProjectReport"/> / <see cref="ProjectReportViewModel"/>.
    /// </summary>
    public interface IProjectSurveyService
    {
        Task<ServiceResult<ProjectReportViewModel>> GetSurveyIndexAsync(int projectId, string? currentUserName, int? templateSectionId = null);
        Task<ServiceResult<ProjectReportViewModel>> IndexPartialAsync(int projectId, int? templateSectionId, int imo);
        Task<ServiceResult<ProjectExportSettings>> PrepareProjectExportSettingsAsync(int projectId, int? templateId, int imo);
        Task<ServiceResult<bool>> CheckShowExportSettingsAsync(int projectId, int imo, int templateId);
        Task<ServiceResult<bool>> CheckShowExportAllSettingsAsync(int projectId, int imo);
        Task<ServiceResult<List<ExportSections>>> GetAllSectionsAsync(int projectId, int templateId);
        Task<ServiceResult<List<ExportPart>>> GetAllPartsAsync(int projectId, int imo);
        Task<ServiceResult<bool>> CreateProjectReportAsync(int projectId, int templateSectionId);
        Task<ServiceResult<ProjectReport>> GetProjectReportAsync(int projectId, int partId, int imo);
        Task<ServiceResult<ProjectReport>> GetProjectReportBySectionIdAsync(int projectId, int partId, Guid sectionId, int imo);
        Task<ServiceResult<ProjectReportViewModel>> GetProjectExportViewModelAsync(int projectId, bool allTemplate, int templateId, int imo, List<ExportPart>? sessionSettings);
        Task<ServiceResult<bool>> SaveProjectReportFileAsync(string id, string sectionId, int totalSections, int templateId, int projectId, byte[] fileContents);
        Task<ServiceResult<MergeReportResultViewModel>> MergeProjectSurveyPdfAsync(string id, bool allTemplate, int projectId, int templateId, int imo, List<ExportPart>? settings);
        Task<ServiceResult<List<UpskillImageData>>> GetBulkUploadImagesListAsync();
        Task<ServiceResult<List<BulkUploadViewModel>>> GetProjectBulkUploadSectionDropdownAsync(int projectId, int imo);
        Task<ServiceResult<SurveyPhotoPlacementViewModel>> GetProjectSurveyPhotoPlacementPartialAsync(int projectId, int templateSectionId);
        Task<ServiceResult<SurveyPhotoPlacementDropdownsViewModel>> GetPhotoPlacementDropdownPartialAsync(int projectId, int templateId, Guid sectionId, int imo);
        Task<ServiceResult<PlaceImageResultDto>> PlaceImageAsync(int projectId, int templateId, Guid sectionId, int imageId, int cardId, string cardName, int descriptionId, int currentConditionId);
        Task<ServiceResult<ProjectReportViewModel>> GetSurveyPdfModelAsync(int projectId, int? templateId, string? sectionId, int imo, int order, bool displayTitle);

        /// <summary>Full-tab validation (MVC <c>ValidateProjectReport</c>). Pass <paramref name="reportSessionState"/> to merge with prior templates; API is stateless unless the client stores this.</summary>
        Task<ServiceResult<string>> ValidateProjectReportAsync(int projectId, int templateSectionId, int imo, string? reportSessionState);

        /// <summary>Previous-tab session string (MVC used server session). Client should send the last <c>reportSessionState</c> value.</summary>
        Task<ServiceResult<string>> ValidateProjectPreviousTabReportAsync(string? reportSessionState);

        Task<ServiceResult<string>> ValidateProjectGradingAsync(int projectId, int templateSectionId, int imo, List<ExportSections>? settings);

        Task<ServiceResult<string>> ExportAllProjectValidationAsync(int projectId, int imo, List<ExportPart> settings);

        Task<ServiceResult<string>> ExportAllProjectPartialValidationAsync(int projectId, int imo, List<ExportPart> settings, int partId);

        Task<ServiceResult<bool>> UpdateHandIGradingNotesAsync(int projectId, int gradingId, string value);

        Task<ServiceResult<bool>> UpdateHandIGradingConditionAsync(int projectId, int gradingId, string choiceName, bool value);
    }
}
