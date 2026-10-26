using DigitalCap.Core.Models.Survey;
using DigitalCap.Core.ViewModels;
using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalCap.Core.Interfaces.Repository
{
    public interface ISurveyReportRepository
    {
        Task<SurveyStatus> MapSurveyStatus(string classNumber, int imo);
        Task<Report> MapStatuatoryCertificates(string classNumber, Report report);
        Task<List<ReportTemplateSection>> GetTemplateSections(int templateId);
        Task<List<ReportTemplate>> GetReportTemplateList();
        Task<List<int>> GetExistingReportPartsSectionIds(int projectId);
        Task<List<ProjectSections>> GetProjectSectionsId(int projectId);
        Task<ReportTemplateUI> GetTemplateTitle(int templateId);
        Task<List<CurrentCondition>> GetCurrentCondition();
        Task<List<ProjectReportMapping>> GetProjectReportTemplate(int projectId, int templateId);
        Task<List<ProjectGradingMapping>> GetProjectGrading(int projectId, int templateId, Guid sectionId);
        Task<List<ProjectGradingConditionMapping>> GetProjectGradingConditionMapping(int projectId, int templateId, Guid sectionId, int gradingId);
        Task<List<GradingUI>> GetTemplatGradings(int templateId, int projectId);
        Task<List<ImageCardUI>> GetImageCard(int templateId);
        Task<List<GradingConditionUI>> GetGradingCondition(int templateId, int projectid);
        Task<List<ProjectCardMapping>> GetProjectImageCard(int projectId, int templateId, Guid sectionId);
        Task<HandIReport> GetHIReport(int projectId);
        Task<ProjectReportMapping> GetProjectReportTemplate(int projectId, int templateId, Guid sectionId);
        Task<List<ReportTemplateSection>> GetTemplatSections(int templateId);
        Task<List<UpskillImageData>> GetBulkUploadUnplacedImages();
        Task<bool> CreateProjectTemplate(ProjectReportMapping model);
        Task<bool> DeleteProjectTemplate(int projectId, int templateId);
        Task InsertBulkUploadImage(int imageId);
        Task<int> DeleteBulkUploadImage(int imageId);
        Task<bool> AddEditImageNew(int imgId, string imageBase64);
        Task<List<string>> GetUnplacedImages(int projectId, string assignmentId);
        Task<string> GetImage(string fileId);
        Task<List<AssignmentsDropdownModel>> GetReportFromAssignments(string assignmentIds);
        Task<bool> DeleteImage(string assignmentId, string fileId);
        Task<bool> DeleteCard(int projectId, string cardId, string sequenceId, string assignmentId, string appId);
        Task<int> UploadImage(string assignmentId, string fileId);
        Task<ProjectCardMapping> GetProjectImagCardById(int projectId, int templateId, Guid sectionId, int cardId);
        Task<bool> CreateProjectImageCard(ProjectCardMapping model);
        Task<bool> UpdateProjectImageCard(ProjectCardMapping model);
        Task<bool> UpdateCardValue(int projectId, string cardId, string sequenceId, string assignmentId, string applicationId, string appName, string label, string value, string gradingJson);
        Task<ProjectGradingMapping> GetProjectGradingById(int projectId, int templateId, Guid sectionId, int gradingId);
        Task<bool> UpdateProjectGrading(ProjectGradingMapping model);
        Task<bool> CreateProjectGrading(ProjectGradingMapping model);
        Task<bool> UpdateProjectGradingConditionMapping(ProjectGradingConditionMapping model);
        Task<bool> CreateProjectGradingConditionMapping(ProjectGradingConditionMapping model);
        Task<bool> UpdateProjectTemplate(ProjectReportMapping model);
        Task<bool> UpdateHIReport(HandIReport model);
        Task<bool> CreateHIReport(HandIReport model);
        Task<bool> InsertVesselMainData(Report report, int projectId);
        Task<bool> UpdateVesselMainData(Report report, int projectId);
    }
}
