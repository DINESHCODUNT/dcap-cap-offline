using DigitalCap.Core.Models;
using DigitalCap.Core.Models.Survey;
using DigitalCap.Core.Models.VesselModel;
using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalCap.Core.Interfaces.Repository
{
    public interface IVesselRepository
    {
       // Task CreateVesselMainDataAsync(Project project, Core.Models.VesselModel.Vessel vessel);
        Task<IEnumerable<SurveyStatus>> GetSurveyStatus(int projectId);
        Task<bool> DeleteSurveyAudit(int projectId);
        Task<IEnumerable<ReportVesselMainData>> GetVesselMainData(int projectId);
        Task<Core.Models.Survey.Certificates> GetStatutoryCertificate(int projectId);
        Task<bool> UpdateStatutoryCertificate(int projectId, Core.Models.Survey.Report report);
        Task<bool> DeleteStatutoryCertificate(int projectId);
        Task<bool> UpdateSurveyAudit(Core.Models.Survey.SurveyStatus surveyStatus, int projectId);

        Task<Core.Models.Vessel?> Details(string imoNumber);
        Task<bool> UpdateReportMainDataAsync(Report report, int projectId);
        Task<string> CreateVesselMainDataAsync(string classNumber, Project project);
        Task<string> UpdateVesselMainDataAsync(string imoNumber, int projectId, DateTime? firstvisit, DateTime? lastvisit);
        Task CreateVesselMainDataAsync(string classNumber, Project project, Core.Models.VesselModel.Vessel vessel);
    }
}
