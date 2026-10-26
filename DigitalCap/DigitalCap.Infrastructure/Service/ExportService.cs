using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.ExportConfig;
using System;
using System.Collections.Generic;
using System.Text;

namespace DigitalCap.Infrastructure.Service
{
    public class ExportService : IExportService
    {
        private readonly IExportRepository _exportRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly ISecurityService _securityService;

        public ExportService(IExportRepository exportRepository, IProjectRepository projectRepository, ISecurityService securityService)
        {
            _exportRepository = exportRepository;
            _projectRepository = projectRepository;
            _securityService = securityService;
        }


        public async Task<ServiceResult<ExportSettings>> GetExportSettingsAsync()
        {
            try
            {
                var vesselTypes = await _exportRepository.GetVesselTypesAsync();
                var imoNumbers = await _exportRepository.GetProjectImoNumbersAsync();

                var settings = new ExportSettings
                {
                    VesselTypes = vesselTypes,
                    ImoNumbers = imoNumbers
                };

                return ServiceResult<ExportSettings>.Success(settings);
            }
            catch
            {
                return ServiceResult<ExportSettings>.Failure("Failed to load export settings");
            }
        }
                     

        public async Task<ServiceResult<ExportSettings>> GetExportPartsByVesselTypeAsync(int vesselTypeId)
        {
            try
            {
                var settings = new ExportSettings();

                // Get Parts
                settings.exportParts =
                    await _exportRepository.GetVesselTypeExportPartConfiguration(vesselTypeId);

                // Get Sections for each Part
                foreach (var part in settings.exportParts)
                {
                    part.sections = await _exportRepository.GetVesselTypeExportSectionConfiguration(part.PartId, vesselTypeId);
                }

                return ServiceResult<ExportSettings>.Success(settings);
            }
            catch (Exception ex)
            {
                // Log exception here if you have logging

                return ServiceResult<ExportSettings>.Failure(
                    $"Failed to load export configuration. {ex.Message}"
                );
            }
        }

        public async Task<ServiceResult<ExportSettings>> GetExportSettingsByProjectAsync(int projectId)
        {
            try
            {
                var settings = new ExportSettings();

                // Get Parts
                settings.exportParts = await _exportRepository.GetProjectExportPartConfiguration(projectId);

                // Get Sections per Part
                foreach (var part in settings.exportParts)
                {
                    part.sections = await _exportRepository.GetProjectExportSectionConfiguration(part.PartId, projectId);
                }

                return ServiceResult<ExportSettings>.Success(settings);
            }
            catch (Exception ex)
            {
                // log ex if needed
                return ServiceResult<ExportSettings>.Failure("Failed to load export configuration");
            }
        }

        public async Task<ServiceResult<bool>> SaveRequiredInExportAsync(ExportSettingsRequest settings,string username)
        {
            try
            {
                foreach (var part in settings.exportParts)
                {
                    
                    await _exportRepository.UpdatePartRequiredInReportAsync(
                        part.RequiredInReport,
                        part.PartId,
                        settings.SelectedImoNumber,
                        settings.SelectedVesselTypeId, username);

                    foreach (var section in part.sections)
                    {
                        if (section.TankTypeId != 0)
                        {
                            await _exportRepository.UpdateTankSectionRequiredInReportAsync(
                                section.RequiredInReport,
                                section.SectionId,
                                settings.SelectedImoNumber, username);
                        }
                        else
                        {
                            await _exportRepository.UpdateSectionRequiredInReportAsync(
                                section.RequiredInReport,
                                section.SectionId,
                                settings.SelectedImoNumber,
                                settings.SelectedVesselTypeId, username);
                        }
                    }
                }

                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                // optionally log ex
                return ServiceResult<bool>.Failure("Failed to save export settings");
            }
        }
                       
        public async Task<ServiceResult<List<Project>>> GetProjectListByIMOAsync(int? imoNumber)
        {
            try
            {
                if (imoNumber.HasValue && imoNumber.Value <= 0)
                {
                    return ServiceResult<List<Project>>.Failure("Invalid IMO number");
                }

                var projects = await _projectRepository.GetProjectListByIMO(imoNumber);

                if (projects == null)
                {
                    return ServiceResult<List<Project>>.Success(new List<Project>());
                }

                return ServiceResult<List<Project>>.Success(projects.ToList());
            }
            catch (Exception ex)
            {
                return ServiceResult<List<Project>>.Failure($"Failed to load projects: {ex.Message}");
            }
        }
    }
}
