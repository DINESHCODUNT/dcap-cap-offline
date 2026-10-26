using DigitalCap.Core.Models;
using DigitalCap.Core.Models.Grading;
using DigitalCap.Core.Models.Tank;
using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalCap.Core.Interfaces.Repository
{
    public interface IGradingRepository
    {
        Task<int> CreateProjectSectionGrading(int projectId, string vesselType);
        Task<bool> CreateVessel_Grading(VesselTankGrading vesselTankGrading);

        Task<List<Grading>> GetAllGradingAsync();
        Task<List<GradingSection>> GetGradingSectionsByTemplateAndVesselAsync(string templateName, string vesselType);
        Task<List<Grading>> CheckGradingNameExistsAsync(string vesselType, string sectionName, string partName, string labelName);
        Task<bool> CreateTankGradingAsync(Grading grading);
        Task<bool> CreateSectionGradingAsync(Grading grading);
        Task<bool> UpdateTankGradingAsync(Grading grading);
        Task<bool> UpdateSectionGradingAsync(Grading grading);
        Task<bool> DeleteGradingAsync(int gradingId, int tankId);
        Task<List<GradingTemplate>> GetGradingTemplatesByVesselType(string Vesseltype);
        Task<List<GradingSection>> GetGradingSectionNamesByTemplateNameAndVesselType(string templateName, string vesseltype);
        Task<List<GradingSection>> GetGradingSections(int templateId, string vesseltype);
        Task<List<GradingTemplate>> GetGradingTemplates();
        //Task<List<GradingResponse>> GetAllGrading_Pagination(int pageNumber, int pageSize);
        Task<PagedResult<Grading>> GetAllGrading_Pagination(int pageNumber, int pageSize);
    }
}
