using DigitalCap.Core.Helpers.Extensions;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Interfaces.Service;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.ExportConfig;
using DigitalCap.Core.Models.ImageDescription;
using DigitalCap.Core.Models.Permissions;
using DigitalCap.Core.Models.ReportConfig;
using DigitalCap.Core.Models.Survey;
using DigitalCap.Core.Models.Tank;
using DigitalCap.Core.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Telerik.Windows.Documents.Fixed.FormatProviders.Pdf.Streaming;
using ImageCards = DigitalCap.Core.Models.Survey.ImageCards;

namespace DigitalCap.Infrastructure.Service
{
    /// <summary>
    /// Project-template survey flow (MVC <c>SurveyController</c>) using <see cref="ProjectReport"/> / report parts.
    /// </summary>
    public partial class ProjectSurveyService : IProjectSurveyService
    {
        private readonly ILogger<ProjectSurveyService> _logger;
        private readonly IConfiguration _configuration;
        private readonly IUserPermissionRepository _userPermissionRepository;
        private readonly ITransferDataOnlinetoOfflineRepository _transferDataOnlinetoOfflineRepository;
        private readonly IUserAccountRepository _userAccountRepository;
        private readonly IProjectRepository _projectRepository;
        private readonly IReportPartRepository _reportPartRepository;
        private readonly IReportPartService _reportPartService;
        private readonly ISurveyReportRepository _surveyReportRepository;
        private readonly ITankRepository _tankRepository;
        private readonly IProjectReportRepository _projectReportRepository;
        private readonly IDescriptionRepository _descriptionRepository;
        private readonly IProjectReportService _projectReportService;
        private readonly IUnitOfWork _unitOfWork;

        public ProjectSurveyService(
            ILogger<ProjectSurveyService> logger,
            IConfiguration configuration,
            IUserPermissionRepository userPermissionRepository,
            ITransferDataOnlinetoOfflineRepository transferDataOnlinetoOfflineRepository,
            IUserAccountRepository userAccountRepository,
            IProjectRepository projectRepository,
            IReportPartRepository reportPartRepository,
            IReportPartService reportPartService,
            ISurveyReportRepository surveyReportRepository,
            ITankRepository tankRepository,
            IProjectReportRepository projectReportRepository,
            IDescriptionRepository descriptionRepository, IProjectReportService projectReportService,
            IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _configuration = configuration;
            _userPermissionRepository = userPermissionRepository;
            _transferDataOnlinetoOfflineRepository = transferDataOnlinetoOfflineRepository;
            _userAccountRepository = userAccountRepository;
            _projectRepository = projectRepository;
            _reportPartRepository = reportPartRepository;
            _reportPartService = reportPartService;
            _surveyReportRepository = surveyReportRepository;
            _tankRepository = tankRepository;
            _projectReportRepository = projectReportRepository;
            _descriptionRepository = descriptionRepository;
            _projectReportService = projectReportService;
        }

        public async Task<ServiceResult<ProjectReportViewModel>> GetSurveyIndexAsync(int projectId, string? currentUserName, int? templateSectionId = null)
        {
            if (string.IsNullOrWhiteSpace(currentUserName))
                return ServiceResult<ProjectReportViewModel>.Failure("Invalid user");

            var defaultTemplateProjectId = _configuration.GetValue<int>("DefaultTemplateProjectId");
            if (projectId < defaultTemplateProjectId)
            {
                return ServiceResult<ProjectReportViewModel>.Failure(
                    "This project uses the legacy survey report. Use the SurveyReport API for this project id.");
            }

            var reportAccess = await _userPermissionRepository.GetRolePermissionByUserName(
                currentUserName,
                EnumExtensions.GetDescription(ManagePages.ReportEdit),
                projectId);

            if (!(Convert.ToBoolean(reportAccess?.Edit) || Convert.ToBoolean(reportAccess?.Read)))
                return ServiceResult<ProjectReportViewModel>.Failure("Access denied");

            var isSync = await _transferDataOnlinetoOfflineRepository.GetDownloadOfflineProjects(projectId);
            if (isSync != null && !string.IsNullOrEmpty(isSync.UserId))
            {
                var userAccount = await _userAccountRepository.GetByAspNetId(isSync.UserId);
                if (userAccount != null)
                    isSync.Name = $"{userAccount.FirstName} {userAccount.LastName}".Trim();
            }

            var project = await _projectRepository.GetProject(projectId);
            if (project == null)
                return ServiceResult<ProjectReportViewModel>.Failure("Project not found");

            var imo = Convert.ToInt32(project.IMO ?? 0);
            var model = await PrepareProjectTemplateViewModelAsync(projectId, null, imo);

            model.Imonumber = project.IMO;
            model.IsRleased = project.IsReadOnlyEnabled;
            model.ReportExport = (await _userPermissionRepository.GetRolePermissionByUserName(
                currentUserName, EnumExtensions.GetDescription(ManagePages.ReportExport), projectId))?.Edit;
            model.ReportExportAll = (await _userPermissionRepository.GetRolePermissionByUserName(
                currentUserName, EnumExtensions.GetDescription(ManagePages.ReportExportAll), projectId))?.Edit;
            model.ResetTemplate = (await _userPermissionRepository.GetRolePermissionByUserName(
                currentUserName, EnumExtensions.GetDescription(ManagePages.ResetTemplate), projectId))?.Edit;

            model.IsSynched = isSync?.IsSynched ?? true;
            if (model.Report != null)
                model.Report.IsSynched = model.IsSynched;
            model.SynchedOnline = isSync;

            return ServiceResult<ProjectReportViewModel>.Success(model);
        }

        public async Task<ServiceResult<ProjectReportViewModel>> IndexPartialAsync(int projectId, int? templateSectionId, int imo)
        {
            var isSync = await _transferDataOnlinetoOfflineRepository.GetDownloadOfflineProjects(projectId);

            // Opening a part writes its ProjectTemplateMapping rows (CreateProjectReportAsync). Offline the report is
            // only read here: the downloaded mappings are used as they are and nothing is written on tab open.
            if (templateSectionId.HasValue && !_unitOfWork.UsesLocalDatabase())
            {
                var pre = await PrepareProjectTemplateViewModelAsync(projectId, null, imo);
                var partRow = pre.ReportPartGrid?.FirstOrDefault(x => x.PartId == templateSectionId.Value);
                if (partRow != null)
                    await CreateProjectReportAsync(projectId, templateSectionId.Value);
            }

            var model = await PrepareProjectTemplateViewModelAsync(projectId, templateSectionId, imo);
            model.IsSynched = isSync?.IsSynched ?? true;
            if (model.Report != null)
            {
                model.Report.IsSynched = model.IsSynched;
                if (templateSectionId.HasValue)
                {
                    model.Report.ShowExportSettings = await _reportPartRepository.CheckToShowSectionExportConfig(
                        projectId, imo, templateSectionId.Value);
                }
            }

            return ServiceResult<ProjectReportViewModel>.Success(model);
        }

        public async Task<ServiceResult<ProjectExportSettings>> PrepareProjectExportSettingsAsync(int projectId, int? templateId, int imo)
        {
            var settings = new ProjectExportSettings();
            var parts = await _reportPartRepository.GetProjectExportPartConfiguration(projectId) ?? new List<ExportPart>();
            
            if (parts.Count == 0)
            {
                // Fallback to default Vessel Type configuration for old projects
                var project = await _projectRepository.GetProject(projectId);
                if (project != null && !string.IsNullOrEmpty(project.VesselType))
                {
                    var vesselTypesList = await _reportPartRepository.GetVesselTypes();
                    var match = vesselTypesList?.vesselTypes?.FirstOrDefault(x => string.Equals(x.VesselType, project.VesselType, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        var defaultParts = await _reportPartRepository.GetVesselTypeExportPartConfiguration(match.VesselTypeId) ?? new List<ExportPart>();
                        foreach (var part in defaultParts)
                        {
                            var exportPart = new ExportPart
                            {
                                PartId = part.PartId,
                                PartName = part.PartName,
                                RequiredInReport = part.RequiredInReport,
                                IncludeInReport = part.IncludeInReport,
                                sections = (await _reportPartRepository.GetVesselTypeExportSectionConfiguration(part.PartId, match.VesselTypeId))
                                    ?.Select(s => new ExportSections
                                    {
                                        SectionId = s.SectionId,
                                        SectionName = s.SectionName,
                                        RequiredInReport = s.RequiredInReport,
                                        IncludeInReport = s.IncludeInReport,
                                        TankTypeId = s.TankTypeId,
                                        IsSubSection = s.IsSubSection,
                                        ParentId = s.ParentId,
                                        TankType = s.TankType
                                    }).ToList() ?? new List<ExportSections>()
                            };
                            parts.Add(exportPart);
                        }
                    }
                }
            }

            if (templateId != null)
                parts = parts.Where(x => x.PartId == templateId).ToList();

            settings.exportParts = parts;
            foreach (var item in settings.exportParts)
            {
                if (item.sections == null || item.sections.Count == 0)
                {
                    item.sections = await _reportPartRepository.GetProjectExportSectionConfiguration(item.PartId, projectId)
                                      ?? new List<ExportSections>();
                }
            }

            return ServiceResult<ProjectExportSettings>.Success(settings);
        }

        public async Task<ServiceResult<bool>> CheckShowExportSettingsAsync(int projectId, int imo, int templateId)
        {
            var show = await _reportPartRepository.CheckToShowSectionExportConfig(projectId, imo, templateId);
            return ServiceResult<bool>.Success(show);
        }

        public async Task<ServiceResult<bool>> CheckShowExportAllSettingsAsync(int projectId, int imo)
        {
            var parts = await _reportPartRepository.GetProjectExportPartConfiguration(projectId) ?? new List<ExportPart>();
            var any = false;
            foreach (var p in parts)
            {
                if (await _reportPartRepository.CheckToShowSectionExportConfig(projectId, imo, p.PartId))
                {
                    any = true;
                    break;
                }
            }

            return ServiceResult<bool>.Success(any);
        }

        public async Task<ServiceResult<List<ExportSections>>> GetAllSectionsAsync(int projectId, int templateId)
        {
            var list = await _reportPartRepository.GetProjectExportSectionConfiguration(templateId, projectId)
                       ?? new List<ExportSections>();
            return ServiceResult<List<ExportSections>>.Success(list);
        }

        public async Task<ServiceResult<List<ExportPart>>> GetAllPartsAsync(int projectId, int imo)
        {
            var settings = await PrepareProjectExportSettingsAsync(projectId, null, imo);
            if (!settings.IsSuccess || settings.Data == null)
                return ServiceResult<List<ExportPart>>.Failure(settings.ErrorMessage ?? "Failed to load parts");
            return ServiceResult<List<ExportPart>>.Success(settings.Data.exportParts ?? new List<ExportPart>());
        }

        public async Task<ServiceResult<bool>> CreateProjectReportAsync(int projectId, int templateSectionId)
        {
            try
            {
                var templatesections = await _surveyReportRepository.GetTemplatSections(templateSectionId);
                foreach (var section in templatesections)
                {
                    var projectReportMapping = new ProjectReportMapping
                    {
                        Id = Guid.NewGuid(),
                        ProjectId = projectId,
                        TemplateId = templateSectionId,
                        IsDeleted = false,
                        CreatedDttm = DateTime.Now,
                        SectionId = section.Id,
                        UpdateDttm = DateTime.Now
                    };
                    await _surveyReportRepository.CreateProjectTemplate(projectReportMapping);
                }

                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CreateProjectReportAsync failed for project {ProjectId}, template {TemplateId}", projectId, templateSectionId);
                return ServiceResult<bool>.Failure("Failed to create project report template");
            }
        }

        public async Task<ServiceResult<ProjectReport>> GetProjectReportAsync(int projectId, int partId, int imo)
        {
            var vm = await GetSectionIdsByProjectIdAsync(projectId, partId, imo);
            var sectionIds = vm.ReportPartGrid?.FirstOrDefault(x => x.PartId == partId)?.SectionIds?
                .Select(x => x.ToString()!).ToList() ?? new List<string>();

            var result = await _reportPartService.GetReportBySectionId(projectId, partId, null, imo, sectionIds);
            if (!result.IsSuccess || result.Data == null)
                return ServiceResult<ProjectReport>.Failure(result.ErrorMessage ?? "Failed to load report");

            return ServiceResult<ProjectReport>.Success(result.Data);
        }

        public async Task<ServiceResult<ProjectReport>> GetProjectReportBySectionIdAsync(int projectId, int partId, Guid sectionId, int imo)
        {
            var vm = await GetSectionIdsByProjectIdAsync(projectId, partId, imo);
            var sectionIds = vm.ReportPartGrid?.FirstOrDefault(x => x.PartId == partId)?.SectionIds?
                .Select(x => x.ToString()!).ToList() ?? new List<string>();

            var result = await _reportPartService.GetReportBySectionId(projectId, partId, sectionId, imo, sectionIds);
            if (!result.IsSuccess || result.Data == null)
                return ServiceResult<ProjectReport>.Failure(result.ErrorMessage ?? "Failed to load report");

            return ServiceResult<ProjectReport>.Success(result.Data);
        }

        public async Task<ServiceResult<ProjectReportViewModel>> GetProjectExportViewModelAsync(
            int projectId, bool allTemplate, int templateId, int imo, List<ExportPart>? sessionSettings)
        {
            var reportVm = await PrepareProjectTemplateViewModelAsync(projectId, null, imo);
            if (!allTemplate)
                reportVm.ReportPartGrid = reportVm.ReportPartGrid?.Where(x => x.PartId == templateId).ToList() ?? new List<ProjectPart>();

            var allparts = reportVm.ReportPartGrid?.ToList() ?? new List<ProjectPart>();
            var settings = sessionSettings ?? new List<ExportPart>();

            if (allparts.Count > 0 && settings.Count > 0)
                allparts = allparts.Where(p => settings.Any(x => x.PartId == p.PartId)).ToList();

            if (allparts.Count > 0 && settings.Count > 0)
            {
                foreach (var part in allparts)
                {
                    var partSettings = settings.FirstOrDefault(x => x.PartId == part.PartId);
                    var temp = partSettings?.sections;
                    if (temp == null || temp.Count == 0)
                        continue;

                    part.SectionIds = part.SectionIds?.Where(s => temp.Any(sub => sub.SectionId == s)).ToList() ?? new List<Guid?>();
                    part.SectionsList = part.SectionsList?.Where(s => temp.Any(sub => sub.SectionId == s.SectionId)).ToList() ?? new List<ProjectGradingSection>();
                }
            }

            var mainDataParts = reportVm.ReportPartGrid?
                .Where(x => x.PartName == "Vessel Main Data" || x.PartName == "Part H&I").ToList() ?? new List<ProjectPart>();
            allparts.AddRange(mainDataParts);
            reportVm.ReportPartGrid = allparts;
            reportVm.Imonumber = imo;
            reportVm.ExportSettings = new ProjectExportSettings { exportParts = settings };

            return ServiceResult<ProjectReportViewModel>.Success(reportVm);
        }

        public async Task<ServiceResult<bool>> SaveProjectReportFileAsync(
            string id, string sectionId, int totalSections, int templateId, int projectId, byte[] fileContents)
        {
            try
            {
                if (id?.Contains("..") == true || id?.Contains('/') == true || id?.Contains('\\') == true)
                    return ServiceResult<bool>.Failure("Invalid path");

                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DigitalCap", "temp", "tempdocument", id);
                var path = Path.Combine(root, templateId.ToString());
                Directory.CreateDirectory(path);

                var fileName = (templateId == 1 || templateId == 2)
                    ? $"{templateId}.pdf"
                    : $"{sectionId}.pdf";

                var filePath = Path.Combine(path, fileName);
                await File.WriteAllBytesAsync(filePath, fileContents);

                var totalFiles = Directory.GetFiles(root, "*.pdf", SearchOption.AllDirectories).Length;
                var completed = totalSections == totalFiles;

                return ServiceResult<bool>.Success(completed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SaveProjectReportFileAsync failed");
                return ServiceResult<bool>.Failure("Failed to save report file");
            }
        }

        public async Task<ServiceResult<MergeReportResultViewModel>> MergeProjectSurveyPdfAsync(
            string id, bool allTemplate, int projectId, int templateId, int imo, List<ExportPart>? settings)
        {
            try
            {
                var surveyReportViewModel = await PrepareProjectTemplateViewModelAsync(projectId, null, imo);
                if (!allTemplate)
                    surveyReportViewModel.ReportPartGrid = surveyReportViewModel.ReportPartGrid?
                        .Where(x => x.PartId == templateId).ToList() ?? new List<ProjectPart>();

                if (settings?.Count > 0)
                {
                    surveyReportViewModel.ReportPartGrid = surveyReportViewModel.ReportPartGrid?
                        .Where(x => settings.Any(s => s.PartId == x.PartId) || x.PartId == 1 || x.PartId == 2).ToList()
                        ?? new List<ProjectPart>();
                }

                var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DigitalCap", "temp", "tempdocument");
                var path = Path.Combine(root, id);
                var documents = Directory.GetFiles(path, "*.pdf", SearchOption.AllDirectories);

                var resultPdfPath = Path.Combine(path, $"{id}_{DateTime.Now:MM_dd_yyyy_HH_mm_ss}.pdf");

                using (var fileWriter = new PdfStreamWriter(File.OpenWrite(resultPdfPath)))
                {
                    foreach (var report in surveyReportViewModel.ReportPartGrid ?? new List<ProjectPart>())
                    {
                        if (report.PartId == 1 || report.PartId == 2)
                            report.SectionIds ??= new List<Guid?>();
                        if (report.PartId == 1 || report.PartId == 2)
                            report.SectionIds.Add(Guid.NewGuid());

                        if (settings?.Count > 0 && report.PartId != 1 && report.PartId != 2)
                        {
                            var sectionSettings = settings.FirstOrDefault(s => s.PartId == report.PartId);
                            if (sectionSettings?.sections != null)
                            {
                                report.SectionIds = report.SectionIds?
                                    .Where(x => sectionSettings.sections.Any(y => y.SectionId == x)).ToList();
                            }
                        }

                        var count = report.SectionIds?.Count ?? 0;
                        for (var i = 0; i <= count - 1; i++)
                        {
                            string sectionFilePath;
                            if (report.PartId == 1 || report.PartId == 2)
                                sectionFilePath = Path.Combine(path, report.PartId.ToString(), $"{report.PartId}.pdf");
                            else
                                sectionFilePath = Path.Combine(path, report.PartId.ToString(), $"{i}.pdf");

                            if (documents.Contains(sectionFilePath))
                            {
                                using var fileToMerge = new PdfFileSource(File.OpenRead(sectionFilePath));
                                foreach (PdfPageSource pageToMerge in fileToMerge.Pages)
                                    fileWriter.WritePage(pageToMerge);
                            }
                        }
                    }
                }

                var base64 = Convert.ToBase64String(await File.ReadAllBytesAsync(resultPdfPath));
                if (!string.IsNullOrEmpty(base64))
                    Directory.Delete(path, true);

                var projectName = await _projectRepository.GetProjectName(projectId) ?? "Project";
                string fileName;
                if (allTemplate)
                    fileName = $"{projectName}_All_Template_{DateTime.Now:MM_dd_yyyy_HH_mm_ss}";
                else
                {
                    var projectDetails = await _projectRepository.GetProject(projectId);
                    var templateTitle = await _reportPartRepository.GetProjectTemplateTitle(templateId, projectId);
                    fileName = $"{projectName}_{projectDetails?.IMO ?? 0}_{templateTitle.PartName}_{DateTime.Now:MM_dd_yyyy_HH_mm_ss}";
                }

                return ServiceResult<MergeReportResultViewModel>.Success(new MergeReportResultViewModel
                {
                    Base64 = base64,
                    FileName = fileName
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MergeProjectSurveyPdfAsync failed");
                return ServiceResult<MergeReportResultViewModel>.Failure("Failed to merge PDF");
            }
        }

        public async Task<ServiceResult<List<UpskillImageData>>> GetBulkUploadImagesListAsync()
        {
            var list = await _surveyReportRepository.GetBulkUploadUnplacedImages();
            return ServiceResult<List<UpskillImageData>>.Success(list ?? new List<UpskillImageData>());
        }

        public async Task<ServiceResult<List<BulkUploadViewModel>>> GetProjectBulkUploadSectionDropdownAsync(int projectId, int imo)
        {
            var projectSections = await GetSectionIdsByProjectIdAsync(projectId, null, imo);
            var result = new List<BulkUploadViewModel>();
            foreach (var obj in projectSections.ReportPartGrid ?? new List<ProjectPart>())
            {
                if (obj.PartName == "Part H&I" || obj.PartName == "Vessel Main Data")
                    continue;

                foreach (var section in obj.SectionsList ?? new List<ProjectGradingSection>())
                {
                    result.Add(new BulkUploadViewModel
                    {
                        Id = $"{obj.PartId}:{section.SectionId}",
                        Name = $"{obj.PartName} : {section.SectionName}"
                    });
                }
            }

            return ServiceResult<List<BulkUploadViewModel>>.Success(result);
        }

        public async Task<ServiceResult<SurveyPhotoPlacementViewModel>> GetProjectSurveyPhotoPlacementPartialAsync(int projectId, int templateSectionId)
        {
            var model = new SurveyPhotoPlacementViewModel
            {
                ProjectId = projectId
            };

            var imoStr = await _projectRepository.GetIMONumberByProjectId(projectId);
            if (int.TryParse(imoStr, out var imo))
                model.imo = imo;

            model.ImageData = await _surveyReportRepository.GetBulkUploadUnplacedImages();
            return ServiceResult<SurveyPhotoPlacementViewModel>.Success(model);
        }

        public async Task<ServiceResult<SurveyPhotoPlacementDropdownsViewModel>> GetPhotoPlacementDropdownPartialAsync(
            int projectId, int templateId, Guid sectionId, int imo)
        {
            try
            {
                var vm = await GetSectionIdsByProjectIdAsync(projectId, templateId, imo);
                var part = vm.ReportPartGrid?.FirstOrDefault(x => x.PartId == templateId);
                var sectionIds = part?.SectionIds ?? new List<Guid?>();
                var model = await BuildProjectPhotoPlacementDropdownsAsync(projectId, templateId, sectionId, imo, sectionIds);
                return ServiceResult<SurveyPhotoPlacementDropdownsViewModel>.Success(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPhotoPlacementDropdownPartialAsync failed");
                return ServiceResult<SurveyPhotoPlacementDropdownsViewModel>.Failure("Failed to load photo placement dropdowns");
            }
        }

        public async Task<ServiceResult<PlaceImageResultDto>> PlaceImageAsync(
            int projectId, int templateId, Guid sectionId, int imageId, int cardId, string cardName, int descriptionId, int currentConditionId)
        {
            try
            {
                var bulkImages = await _surveyReportRepository.GetBulkUploadUnplacedImages();
                var bulkImage = bulkImages?.FirstOrDefault(x => x.ImageId == imageId);
                if (bulkImage == null)
                    return ServiceResult<PlaceImageResultDto>.Failure("Image not found");

                var normalSections = await _reportPartRepository.GetNormalSectionByProjectId(projectId, templateId);
                var isNormalSection = normalSections?.Any(x => x.SectionMappingId == sectionId) == true;

                if (isNormalSection)
                    return await UpdateNonTankGenericImageAsync(projectId, templateId, sectionId, imageId, cardId, cardName, descriptionId, currentConditionId, bulkImage);

                return await UpdateTankImageAsync(projectId, templateId, sectionId, imageId, cardId, cardName, descriptionId, currentConditionId, bulkImage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PlaceImageAsync failed");
                return ServiceResult<PlaceImageResultDto>.Failure("Failed to place image");
            }
        }

        public async Task<ServiceResult<ProjectReportViewModel>> GetSurveyPdfModelAsync(
            int projectId, int? templateId, string? sectionId, int imo, int order, bool displayTitle)
        {
            ProjectReportViewModel reportSectionModel;
            try
            {
                reportSectionModel = await GetSectionIdsByProjectIdAsync(projectId, templateId, imo);
            }
            catch
            {
                reportSectionModel = new ProjectReportViewModel();
            }

            if (templateId == null)
                return ServiceResult<ProjectReportViewModel>.Failure("templateId is required");

            var part = reportSectionModel.ReportPartGrid?.FirstOrDefault(x => x.PartId == templateId.Value);
            var templateSectionIds = part?.SectionIds ?? new List<Guid?>();
            var sectionIds = templateSectionIds.Count > 0
                ? templateSectionIds.Select(x => x.ToString()!).ToList()
                : new List<string>();

            ServiceResult<ProjectReport> ret;
            if (templateId == 1 || templateId == 2)
                ret = await _reportPartService.GetReportBySectionId(projectId, templateId.Value, null, imo, sectionIds);
            else
            {
                if (string.IsNullOrEmpty(sectionId) || !Guid.TryParse(sectionId, out var sid))
                    return ServiceResult<ProjectReportViewModel>.Failure("sectionId is required for this template");
                ret = await _reportPartService.GetReportBySectionId(projectId, templateId.Value, sid, imo, sectionIds);
            }

            if (!ret.IsSuccess || ret.Data == null)
                return ServiceResult<ProjectReportViewModel>.Failure(ret.ErrorMessage ?? "Failed to load report");

            ret.Data.Order = order;

            var projectReportViewModel = new ProjectReportViewModel
            {
                Report = ret.Data,
                ProjectId = projectId,
                ProjectName = ret.Data.ProjectName,
                DisplayTitle = displayTitle
            };

            return ServiceResult<ProjectReportViewModel>.Success(projectReportViewModel);
        }

        private async Task<ProjectReportViewModel> PrepareProjectTemplateViewModelAsync(int projectId, int? templateSectionId, int imonumber)
        {
            var model = await GetSectionIdsByProjectIdAsync(projectId, templateSectionId, imonumber);
            model.Report ??= new ProjectReport();
            model.Report.template ??= new ProjectView();

            model.ProjectName = await _projectRepository.GetProjectName(projectId);

            if (templateSectionId != null)
            {
                try
                {
                    List<string> sectionIds = null;

                    model.Report = await _projectReportService.GetReport(projectId, templateSectionId.Value, imonumber, sectionIds);
                    if (templateSectionId == 1)
                    {

                        model.Report.ProjectVesselType = _projectRepository.GetProjectVesselType(projectId).Result;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "PrepareProjectTemplateViewModelAsync load report failed");
                }

                //try
                //{
                //    var sectionIds = model.ReportPartGrid?
                //        .FirstOrDefault(x => x.PartId == templateSectionId.Value)?
                //        .SectionIds?.Select(x => x.ToString()!).ToList() ?? new List<string>();

                //    var reportResult = await _reportPartService.GetReportBySectionId(
                //        projectId, templateSectionId.Value, null, imonumber, sectionIds);

                //    if (reportResult.IsSuccess && reportResult.Data != null)
                //    {
                //        model.Report = reportResult.Data;
                //        if (templateSectionId == 1)
                //            model.Report.ProjectVesselType = await _projectRepository.GetProjectVesselType(projectId);
                //    }
                //}
                //catch (Exception ex)
                //{
                //    _logger.LogWarning(ex, "PrepareProjectTemplateViewModelAsync load report failed");
                //}
            }
            else
            {
                model.UnplacedImages = await _surveyReportRepository.GetBulkUploadUnplacedImages();
            }

            return model;
        }

        private async Task<ProjectReportViewModel> GetSectionIdsByProjectIdAsync(int projectId, int? templateId, int? imo)
        {
            var model = new ProjectReportViewModel();
            model.ProjectId = projectId;
            model.Report = new ProjectReport();
            model.Report.template = new ProjectView();


            var allSectionTemplates = _projectReportRepository.GetProjectTemplatebyProjectId(projectId).Result;


            model.ReportPartGrid = new List<ProjectPart>();

            if (templateId != null && templateId != 0)
                allSectionTemplates = allSectionTemplates.Where(x => x.PartId == templateId.Value).ToList();
            foreach (var template in allSectionTemplates)
            {

                List<ProjectGradingSection> Sections = new List<ProjectGradingSection>();
                var sections = _reportPartRepository.GetNormalSectionByProjectId(projectId, template.PartId).Result;

                //    sections = sections.Where(x => x.HasSubSection == false).ToList();
                if (sections?.Count > 0)
                {
                    Sections.AddRange(sections.Select(x => new ProjectGradingSection() { SectionId = x.SectionMappingId, SectionName = x.SectionName, Order = x.Order }));
                    foreach (var section in sections)
                    {
                        if (section.HasSubsection)
                        {
                            var subsection = _reportPartRepository.GetProjectSubsectionBySectionId(section.SectionMappingId).Result;
                            Sections.AddRange(subsection.Select(x => new ProjectGradingSection() { SectionId = x.SectionMappingId, SectionName = x.SectionName, Order = x.Order, IsSubSection = true, TankTypeId = 0 }));
                        }
                    }
                }


                // Fetch tank sections for this part
                var tanks = _reportPartRepository.GetTankSectionByProjectId(projectId, template.PartId, Convert.ToInt32(imo)).Result;

                // Use max order of normal sections so tanks always sort AFTER normal sections.
                // Each tank gets a unique offset to preserve its own DB order.
                var max_normal_order = Sections.Count > 0 ? Sections.Max(x => x.Order) : 0;
                if (tanks?.Count > 0)
                {
                    for (int ti = 0; ti < tanks.Count; ti++)
                    {
                        Sections.Add(new ProjectGradingSection
                        {
                            SectionId = tanks[ti].Id,
                            SectionName = tanks[ti].TankName,
                            Order = max_normal_order + ti + 1,  // after all normal sections, in DB order
                            TankTypeId = tanks[ti].TankTypeId
                        });
                    }
                }


                var reportPart = new ProjectPart()
                {
                    PartId = template.PartId,
                    PartName = template.PartName,
                    SectionIds = Sections.OrderBy(x => x.Order).Select(x => x.SectionId).ToList(),
                    SectionsList = Sections.OrderBy(x => x.Order).ToList(),
                };
                model.ReportPartGrid.Add(reportPart);

            }
            return model;
        }

        private async Task<ServiceResult<PlaceImageResultDto>> UpdateTankImageAsync(
            int projectId, int templateId, Guid sectionId, int imageId, int cardId, string cardName, int descriptionId, int currentConditionId, UpskillImageData bulkImage)
        {
            cardName = cardName.Trim();
            var imageCard = await _tankRepository.GetProjectTankImagCardByName(projectId, templateId, sectionId, cardId);
            if (imageCard == null)
                imageCard = new TankImageCard();

            if (imageCard.Id == 0)
            {
                imageCard.ProjectId = projectId;
                imageCard.TemplateId = templateId;
                imageCard.SectionId = sectionId;
                imageCard.CardNumber = cardId;
                imageCard.CardName = cardName;
                imageCard.DescriptionId = descriptionId;
                imageCard.CurrentCondition = currentConditionId;
                imageCard.CreatedDttm = DateTime.Now;
                imageCard.UpdatedDttm = DateTime.Now;
                imageCard.ImageId = imageId;
                imageCard.IsActive = true;
                var result = await _tankRepository.CreateProjectTankImageCard(imageCard);
                if (result)
                    await _surveyReportRepository.DeleteBulkUploadImage(imageId);
                return ServiceResult<PlaceImageResultDto>.Success(new PlaceImageResultDto { IsSuccess = result, ImageData = bulkImage.Base64String });
            }

            imageCard.DescriptionId = (imageCard.DescriptionId != descriptionId) ? descriptionId : imageCard.DescriptionId;
            imageCard.CurrentCondition = (imageCard.CurrentCondition != currentConditionId) ? currentConditionId : imageCard.CurrentCondition;
            imageCard.ImageId = imageId;
            imageCard.UpdatedDttm = DateTime.Now;
            var updated = await _tankRepository.UpdateProjectTankImageCard(imageCard);
            if (updated)
                await _surveyReportRepository.DeleteBulkUploadImage(imageId);
            return ServiceResult<PlaceImageResultDto>.Success(new PlaceImageResultDto { IsSuccess = updated, ImageData = bulkImage.Base64String });
        }

        private async Task<ServiceResult<PlaceImageResultDto>> UpdateNonTankGenericImageAsync(
            int projectId, int templateId, Guid sectionId, int imageId, int cardId, string cardName, int descriptionId, int currentConditionId, UpskillImageData bulkImage)
        {
            cardName = cardName.Trim();
            var imageCard = await _projectReportRepository.GetGenericImagCardByName(projectId, templateId, sectionId, cardId);
            if (imageCard == null)
                imageCard = new GenericImageCard();

            if (imageCard.Id == 0)
            {
                imageCard.ProjectId = projectId;
                imageCard.TemplateId = templateId;
                imageCard.SectionId = sectionId;
                imageCard.CardNumber = cardId;
                imageCard.CardName = cardName;
                imageCard.DescriptionId = descriptionId;
                imageCard.CurrentCondition = currentConditionId;
                imageCard.CreatedDttm = DateTime.Now;
                imageCard.UpdatedDttm = DateTime.Now;
                imageCard.ImageId = imageId;
                imageCard.IsActive = true;
                var result = await _projectReportRepository.CreateGenericImageCard(imageCard);
                if (result)
                    await _surveyReportRepository.DeleteBulkUploadImage(imageId);
                return ServiceResult<PlaceImageResultDto>.Success(new PlaceImageResultDto { IsSuccess = result, ImageData = bulkImage.Base64String });
            }

            imageCard.CardName = cardName;
            imageCard.DescriptionId = (imageCard.DescriptionId != descriptionId) ? descriptionId : imageCard.DescriptionId;
            imageCard.CurrentCondition = (imageCard.CurrentCondition != currentConditionId) ? currentConditionId : imageCard.CurrentCondition;
            imageCard.ImageId = imageId;
            imageCard.UpdatedDttm = DateTime.Now;
            var updated = await _projectReportRepository.UpdateGenericImageCard(imageCard);
            if (updated)
                await _surveyReportRepository.DeleteBulkUploadImage(imageId);
            return ServiceResult<PlaceImageResultDto>.Success(new PlaceImageResultDto { IsSuccess = updated, ImageData = bulkImage.Base64String });
        }

        private async Task<SurveyPhotoPlacementDropdownsViewModel> BuildProjectPhotoPlacementDropdownsAsync(
            int projectId, int templateId, Guid sectionId, int imo, List<Guid?> sectionIdsOrdered)
        {
            var model = new SurveyPhotoPlacementDropdownsViewModel
            {
                ProjectId = projectId,
                TemplateId = templateId,
                SectionId = sectionId,
                ImageDescription = new List<ImageDescriptionUI>(),
                ImageCard = new List<ImageCards>(),
                CurrentCondition = await _surveyReportRepository.GetCurrentCondition() ?? new List<CurrentCondition>()
            };

            var partTitle = await _reportPartRepository.GetProjectTemplateTitle(templateId, projectId);
            if (partTitle.PartName == "Vessel Main Data" || partTitle.PartName == "Part H&I")
                return model;

            var tanks = await _reportPartRepository.GetTankSectionByProjectId(projectId, templateId, imo);
            var tank = tanks?.FirstOrDefault(t => t.Id == sectionId);

            var imagedescriptions = await _descriptionRepository.GetImageDescriptionsByProjectId(projectId) ?? new List<ImageDescriptions>();

            if (tank != null)
            {
                model.ImageDescription = imagedescriptions
                    .Where(x => x.TankTypeId == tank.TankTypeId)
                    .Select(x => new ImageDescriptionUI { Id = x.Id, Description = x.Description })
                    .ToList();

                var tankCards = await _tankRepository.GetProjectTankImageCard(projectId, templateId, tank.Id) ?? new List<TankImageCard>();
                var index = (sectionIdsOrdered?.IndexOf(sectionId) ?? -1) + 1;
                if (index < 1)
                    index = 1;
                var templateLetter = templateId switch
                {
                    2 => "B",
                    3 => "C",
                    4 => "D",
                    _ => "T"
                };
                var emptyCards = GetEmptyTankImageCards(tankCards, templateLetter, index.ToString(), tank);
                model.ImageCard = emptyCards;
                return model;
            }

            model.ImageDescription = imagedescriptions
                .Where(x => x.SectionId == sectionId)
                .Select(x => new ImageDescriptionUI { Id = x.Id, Description = x.Description })
                .ToList();

            var existing = await _reportPartRepository.GetGenericImageCard(projectId, templateId, sectionId) ?? new List<GenericImageCard>();
            var sectionMeta = (await _reportPartRepository.GetNormalSectionByProjectId(projectId, templateId))
                ?.FirstOrDefault(x => x.SectionMappingId == sectionId);

            var total = sectionMeta?.Totalcards > 0 ? sectionMeta.Totalcards : 32;
            var imageCard = new List<ImageCards>();
            for (var n = 1; n <= total; n++)
            {
                imageCard.Add(new ImageCards
                {
                    Id = n,
                    FileName = $"{sectionMeta?.SectionName ?? "Card"}-{n}",
                    CurrentCondition = n > (sectionMeta?.PlaceholderCount ?? 16)
                });
            }

            model.ImageCard = imageCard
                .Where(x => !existing.Any(y => y.CardNumber == x.Id && !string.IsNullOrEmpty(y.src)))
                .ToList();

            return model;
        }

        private static List<ImageCards> GetEmptyTankImageCards(List<TankImageCard> existing, string templateName, string index, ProjectTankUI tank)
        {
            var imageCards = Enumerable.Range(1, 32)
                .Select(x => new TankImageCard
                {
                    CardNumber = x,
                    CardName = templateName + index + "-" + x,
                    CurrentCondition = (x > 16) ? 1 : 0
                }).ToList();

            if (existing.Count > 0)
                imageCards = imageCards.Where(ec => !existing.Any(ex => ex.CardNumber == ec.CardNumber && ex.ImageId > 0)).ToList();

            return imageCards.Select(x => new ImageCards
            {
                Id = x.CardNumber,
                FileName = x.CardName,
                CurrentCondition = x.CardNumber > tank.PlaceholderCount
            }).OrderBy(x => x.Id).ToList();
        }
    }
}
