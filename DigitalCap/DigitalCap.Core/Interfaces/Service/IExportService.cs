using DigitalCap.Core.Models;
using DigitalCap.Core.Models.ExportConfig;
using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalCap.Core.Interfaces.Service
{
    public interface IExportService
    {
        Task<ServiceResult<ExportSettings>> GetExportSettingsAsync();
        Task<ServiceResult<ExportSettings>> GetExportPartsByVesselTypeAsync(int vesselTypeId);
        Task<ServiceResult<ExportSettings>> GetExportSettingsByProjectAsync(int projectId);
        Task<ServiceResult<bool>> SaveRequiredInExportAsync(ExportSettingsRequest settings,string username);
        Task<ServiceResult<List<Project>>> GetProjectListByIMOAsync(int? imoNumber);
    }
}
