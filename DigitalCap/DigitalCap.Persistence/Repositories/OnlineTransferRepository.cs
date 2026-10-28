using Dapper;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.DataTransfer;
using DigitalCap.Core.Models.ImageDescription;
using DigitalCap.Core.Models.ReportConfig;
using DigitalCap.Core.Models.Survey;
using DigitalCap.Core.Models.Tank;
using DigitalCap.Core.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalCap.Persistence.Repositories
{
    /// <summary>
    /// Direct Azure SQL port of the reference online server's DataTransferDevAPI
    /// (digital-cap TransferDataOnlinetoOfflineRepository / SyncTransferDataOfflinetoOnlineRepository).
    /// Stored procedure names and SQL text are kept identical to the reference. Every call opens its own
    /// SqlConnection so it is unaffected by the online/offline switching of the unit of work.
    /// </summary>
    public class OnlineTransferRepository : IOnlineTransferRepository
    {
        // Stored procedures (reference digital-cap Utils/Constants/StoredProcedure.cs, class DataTransfer).
        private const string SpGetProject = "dbo.DataTransferProjects";
        private const string SpGetDownloadedProject = "dbo.GetDownloadOfflineProject";
        private const string SpAddDownloadedProject = "dbo.AddDownloadOfflineProject";
        private const string SpReleaseErrorSyncData = "dbo.ReleaseErrorSyncData";
        private const string SpReleaseSyncData = "dbo.ReleaseSyncData";
        private const string SpMoveToHistoryTable = "dbo.MoveToHistoryTable";
        private const string SpMoveToOnlineTable = "[dbo].[MoveToOnlineTable]";
        private const string SpGetComments = "dbo.DataTransferComments";
        private const string SpGetGrading = "dbo.DataTransferGrading";
        private const string SpGetImageDescriptions = "dbo.DataTransferImageDescriptions";
        private const string SpGetVesselGradingMapping = "dbo.DataTransferVessel_GradingMapping";
        private const string SpGetVesselTankMappingByProject = "[dbo].[DataTransferVessel_TankMappingByProject]";
        private const string SpGetHIReportTemplate = "dbo.GetH_IReportTemplateById";
        private const string SpGetAllSections = "[dbo].[GetAllSectionByProjectId]";
        private const string SpGetProjectGradingConditionMappingByTemplateId = "[dbo].[DataTransferProjectGradingConditionMappingByTemplateId]";
        private const string SpGetProjectGradingMappingByTemplateId = "[dbo].[DataTransferProjectGradingMappingByTemplateId]";
        private const string SpGetProjectImageCardByTemplateId = "[dbo].[DataTransferProjectImageCardsByTemplateId]";
        private const string SpGetGenericImageCardByTemplateId = "[dbo].[DataTransferGenericImageCardByTemplateId]";
        private const string SpGetHandIProjectGradingById = "[dbo].[DataTransferGetHandIProjectGradingById]";
        private const string SpGetHandIProjectGradingConditionById = "[dbo].[DataTransferGetHandIProjectGradingConditionById]";
        private const string SpGetImageByTemplateId = "[dbo].[DataTransferImageByTemplateId]";
        private const string SpGetImageDataByTemplateId = "[dbo].[DataTransferImageDataByTemplateId]";
        private const string SpGetTankImageCardByTemplateId = "[dbo].[DataTransferTankImageCardsByTemplateId]";
        private const string SpGetProjectTemplateMappingByTemplateId = "[dbo].[DataTransferProjectTemplateMappingByTemplateId]";
        private const string SpGetCapActivity = "[dbo].[DataTransferActivities]";
        private const string SpGetProjectGrades = "dbo.DataTransferProjectGrades";
        private const string SpReportVesselMainData = "dbo.GetReportVesselMainDataById";
        private const string SpGetReportSurveyDetails = "dbo.GetReportSurveyDetailsById";
        private const string SpGetReportStatutoryCertificates = "dbo.GetReportStatutoryCertificatesById";
        // Offline permissions (reference digital-cap StoredProcedure.RolePermission) and the workflow tasks.
        private const string SpGetPermissionOffline = "dbo.GetPermissionOffline";
        private const string SpGetCurrentUserRole = "dbo.GetCurrentUserRole";
        private const string SpGetCap2TasksByProjectId = "[CAP].[Read_Cap2Tasks_ByProjectId]";

        // Large projects carry base64 image data; the default 30s is too short for those reads/writes.
        private const int CommandTimeoutSeconds = 600;

        private readonly string _connectionString;

        public OnlineTransferRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("ABSConnectionString")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? string.Empty;
        }

        private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
        {
            var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        private static CommandDefinition Sp(string name, object? parameters, CancellationToken ct, IDbTransaction? tx = null)
            => new CommandDefinition(name, parameters, tx, CommandTimeoutSeconds, CommandType.StoredProcedure, cancellationToken: ct);

        private static CommandDefinition Text(string sql, object? parameters, CancellationToken ct, IDbTransaction? tx = null)
            => new CommandDefinition(sql, parameters, tx, CommandTimeoutSeconds, CommandType.Text, cancellationToken: ct);

        // ---------------------------------------------------------------- marker

        public async Task<IsSynchedOnline?> GetDownloadedProjectAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            return await connection.QueryFirstOrDefaultAsync<IsSynchedOnline>(Sp(SpGetDownloadedProject, new { ProjectId = projectId }, cancellationToken));
        }

        public async Task AddDownloadedProjectAsync(int projectId, string userId, string userName, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);

            // The reference stores the AspNetUsers Id here (from LogInData), which the server may join on to
            // show who holds the project; the Azure AD object id is only a fallback for users without one.
            var aspNetUserId = await connection.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
                "SELECT TOP 1 Id FROM dbo.AspNetUsers WHERE NormalizedEmail = UPPER(@Email) OR NormalizedUserName = UPPER(@Email)",
                new { Email = userName },
                cancellationToken: cancellationToken));

            // Reference AddDownloadOfflineProjects: @ProjectId, @UserId, @UserName (= the user's email).
            var parameters = new DynamicParameters();
            parameters.Add("@ProjectId", projectId);
            parameters.Add("@UserId", string.IsNullOrWhiteSpace(aspNetUserId) ? userId : aspNetUserId);
            parameters.Add("@UserName", userName);

            await connection.ExecuteAsync(Sp(SpAddDownloadedProject, parameters, cancellationToken));
        }

        // ---------------------------------------------------------------- download reads

        public async Task<TransferDataModel?> GetProjectDataAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            var byProject = new { ProjectId = projectId };
            var byId = new { Id = projectId };

            var project = await connection.QueryFirstOrDefaultAsync<ProjectDataTransfer>(Sp(SpGetProject, byProject, cancellationToken));
            if (project == null)
                return null;

            var model = new TransferDataModel
            {
                ProjectId = projectId,
                Project = project,
                VesselMainData = await ListSp<GetReportVesselMainData>(connection, SpReportVesselMainData, byProject, cancellationToken),
                SurveyDetails = await ListSp<ReportSurveyDetails>(connection, SpGetReportSurveyDetails, byProject, cancellationToken),
                StatutoryCertificates = await ListSp<TransferReportStatutoryCertificates>(connection, SpGetReportStatutoryCertificates, byProject, cancellationToken),
                HIReport = (await connection.QueryFirstOrDefaultAsync<HIReportTemplate>(Sp(SpGetHIReportTemplate, byProject, cancellationToken)))!, // null when the project has none
                Activities = await ListSp<global::CapActivity>(connection, SpGetCapActivity, byProject, cancellationToken),
                handIProjectGradings = await ListSp<HandIProjectGrading>(connection, SpGetHandIProjectGradingById, byProject, cancellationToken),
                handIProjectGradingConditions = await ListSp<HandIProjectGradingConditions>(connection, SpGetHandIProjectGradingConditionById, byProject, cancellationToken),
                comments = await GetCommentsAsync(connection, projectId, cancellationToken),
                projectGrades = await ListSp<CapProjectGrade>(connection, SpGetProjectGrades, byProject, cancellationToken),
                gradingData = await ListSp<GradingData>(connection, SpGetGrading, byProject, cancellationToken),
                imageDescriptions = await ListSp<ImageDescriptionsData>(connection, SpGetImageDescriptions, byProject, cancellationToken),
                gradingMapping = await ListSp<VesselGradingMapping>(connection, SpGetVesselGradingMapping, byProject, cancellationToken),
                vesselTankMapping = await ListSp<VesselTankMapping>(connection, SpGetVesselTankMappingByProject, byProject, cancellationToken),
                ReportPart = await ListText<TransferReportPart>(connection,
                    "Select * from config.ReportPart where projectId=@Id and IsDeleted=0 and IsActive=1", byId, cancellationToken),
                ProjectTankSection = await ListText<ProjectTankSectionMapping>(connection,
                    "Select * from [Config].[ProjectTankSectionMapping] where projectId=@Id and IsDeleted=0 and IsActive=1", byId, cancellationToken),
                ProjectSectionMapping = await ListText<ProjectSectionMapping>(connection,
                    "Select * from [Config].[ProjectSectionMapping] where projectId=@Id and IsDeleted=0 and IsActive=1", byId, cancellationToken),
                ProjectSubSection = await ListText<DigitalCap.Core.Models.ProjectSubSection>(connection,
                    @"Select PSS.* from [Config].[ProjectSubSection] PSS
                inner join [Config].[ProjectSectionMapping] PS on PSS.ProjectSectionId=PS.SectionMappingId
                and  ps.ProjectId=@Id and ps.HasSubSection=1 and PS.IsDeleted=0 and PS.IsActive=1 and PSS.IsActive=1 and PSS.IsDeleted=0", byId, cancellationToken),
                ProjectSectionGrading = await ListText<ProjectSectionGradingMapping>(connection,
                    @"Select  PSS.* from [Config].[ProjectSectionGradingMapping] PSS
                inner join [Config].[ProjectSectionMapping] PS on PSS.SectionId=PS.SectionMappingId
                and  ps.ProjectId=@Id  and PS.IsDeleted=0 and PS.IsActive=1 and PSS.IsActive=1 and PSS.IsDeleted=0
				Union
				Select  PSG.* from [Config].[ProjectSectionMapping] PS
                inner join [Config].[ProjectSubSection] PSS on PSS.ProjectSectionId=PS.SectionMappingId and PS.HasSubSection=1
				inner JOin [Config].[ProjectSectionGradingMapping] PSG on PSS.Id=PSG.SectionId
                and  ps.ProjectId=@Id  and PS.IsDeleted=0 and PS.IsActive=1 and PSS.IsActive=1 and PSS.IsDeleted=0", byId, cancellationToken),
                ProjectSectionDescription = await ListText<ProjectSectionDescriptionMapping>(connection,
                    @"Select  PSS.* from [Config].[ProjectSectionDescriptionMapping] PSS
                inner join [Config].[ProjectSectionMapping] PS on PSS.SectionId=PS.SectionMappingId
                and  ps.ProjectId=@Id  and PS.IsDeleted=0 and PS.IsActive=1 and PSS.IsActive=1 and PSS.IsDeleted=0
				", byId, cancellationToken),
                ProjectSubSectionDescription = await ListText<ProjectSubSectionDescriptionMapping>(connection,
                    @"Select  PSG.* from [Config].[ProjectSubSection] PSS
				inner join [Config].[ProjectSectionMapping] Psm on psm.SectionMappingId=pss.ProjectSectionId
				inner JOin [Config].[ProjectSubSectionDescriptionMapping] PSG on PSS.Id=PSG.SubSectionId
                and  psm.ProjectId=@Id and PSg.IsDeleted=0 and PSg.IsActive=1 and PSS.IsActive=1 and PSS.IsDeleted=0
				and psm.IsActive=1 and psm.IsDeleted=0
				", byId, cancellationToken),
                VesselTypeTankMapping = await ListText<VesselTypeTankMapping>(connection,
                    @"SELECT DISTINCT vtank.*
FROM vesseltype v
INNER JOIN config.VesselTypePartMapping vpart
    ON vpart.VesselTypeId = v.Id
    AND vpart.IsDeleted = 0
    AND vpart.IsActive = 1
INNER JOIN config.VesselTypeTankSectionMapping vtank
    ON vtank.VesselTypePartMappingId = vpart.VesselTypePartMappingId
    AND vtank.IsDeleted = 0
    AND vtank.IsActive = 1
WHERE v.VesselType = @vesseltype
ORDER BY vtank.TankTypeId;

				", new { vesseltype = project.VesselType }, cancellationToken),
            };

            return model;
        }

        public async Task<List<SingleTask>> GetProjectTasksAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            // Same procedure TaskRepository.GetCap2Tasks uses online.
            return await ListSp<SingleTask>(connection, SpGetCap2TasksByProjectId, new { ID = projectId }, cancellationToken);
        }

        public async Task<OfflineUserSecurity> GetUserSecurityAsync(string userName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(userName))
                throw new ArgumentException("A user name is required to read the user's permissions.", nameof(userName));

            await using var connection = await OpenAsync(cancellationToken);

            var aspNetUserId = await connection.QueryFirstOrDefaultAsync<string>(Text(
                "SELECT TOP 1 Id FROM dbo.AspNetUsers WHERE NormalizedEmail = UPPER(@Email) OR NormalizedUserName = UPPER(@Email)",
                new { Email = userName }, cancellationToken));

            // Reference UserPermissionRepository.GetcurrentUserRoles: first column of each row is the role name.
            var roles = (await connection.QueryAsync<string>(Sp(SpGetCurrentUserRole, new { username = userName }, cancellationToken)))
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // Permission claims as SecurityService.MapRolesAndClaimsToUser collects them (role claims + user claims).
            var claims = aspNetUserId == null
                ? new List<string>()
                : (await connection.QueryAsync<string>(Text(@"
                    SELECT rc.ClaimType FROM dbo.AspNetRoleClaims rc
                        INNER JOIN dbo.AspNetUserRoles ur ON ur.RoleId = rc.RoleId
                        WHERE ur.UserId = @UserId
                    UNION
                    SELECT uc.ClaimType FROM dbo.AspNetUserClaims uc WHERE uc.UserId = @UserId",
                    new { UserId = aspNetUserId }, cancellationToken)))
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToList();

            // Reference UserPermissionRepository.GetOfflinePermissions (RoleName, PermissionName, Read, Edit, Delete).
            var rolePermissions = await ListSp<OfflineRolePermission>(connection, SpGetPermissionOffline, null!, cancellationToken);

            return new OfflineUserSecurity
            {
                UserName = userName,
                AspNetUserId = aspNetUserId,
                Roles = roles,
                Claims = claims,
                RolePermissions = rolePermissions
            };
        }

        public async Task<List<AllSections>> GetAllSectionsAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            return await ListSp<AllSections>(connection, SpGetAllSections, new { ProjectId = projectId }, cancellationToken);
        }

        public async Task<TransferDataModel> GetSectionDataAsync(int projectId, int templateId, Guid sectionId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            var p = new { ProjectId = projectId, TemplateId = templateId, SectionId = sectionId };

            return new TransferDataModel
            {
                ProjectId = projectId,
                gradingGradingMapping = await ListSp<ProjectGradingMapping>(connection, SpGetProjectGradingMappingByTemplateId, p, cancellationToken),
                projectGradingConditionMappings = await ListSp<ProjectGradingConditionMapping>(connection, SpGetProjectGradingConditionMappingByTemplateId, p, cancellationToken),
                reportMapping = await ListSp<ProjectReportMapping>(connection, SpGetProjectTemplateMappingByTemplateId, p, cancellationToken),
                projectCardMappings = await ListSp<ProjectCardMapping>(connection, SpGetProjectImageCardByTemplateId, p, cancellationToken),
                genericImageCard = await ListSp<GenericImageCard>(connection, SpGetGenericImageCardByTemplateId, p, cancellationToken),
                tankImageCards = await ListSp<TankImageCard>(connection, SpGetTankImageCardByTemplateId, p, cancellationToken),
                images = await ListSp<Images>(connection, SpGetImageByTemplateId, p, cancellationToken),
                upskillImageData = await ListSp<UpskillImageData>(connection, SpGetImageDataByTemplateId, p, cancellationToken),
            };
        }

        private static async Task<List<Comment>> GetCommentsAsync(SqlConnection connection, int projectId, CancellationToken ct)
        {
            // Same mapping as the reference GetCommentsById (LastModifiedDate formatted for SQLite).
            var rows = await connection.QueryAsync<dynamic>(Sp(SpGetComments, new { ProjectId = projectId }, ct));
            var comments = new List<Comment>();
            foreach (var item in rows)
            {
                comments.Add(new Comment
                {
                    Id = item.Id != null ? (int)item.Id : 0,
                    ProjectId = item.ProjectId != null ? (int)item.ProjectId : 0,
                    ProjectTaskId = item.ProjectTaskId != null ? (int)item.ProjectTaskId : 0,
                    CommentText = item.CommentText != null ? (string)item.CommentText : string.Empty,
                    CreatedBy = item.CreatedBy != null ? (string)item.CreatedBy : string.Empty,
                    LastModifiedDate = item.LastModifiedDate != null ? ((DateTimeOffset)item.LastModifiedDate).ToString("yyyy-MM-dd HH:mm:ss") : string.Empty
                });
            }
            return comments;
        }

        private static async Task<List<T>> ListSp<T>(SqlConnection connection, string sp, object parameters, CancellationToken ct)
            => (await connection.QueryAsync<T>(Sp(sp, parameters, ct))).AsList();

        private static async Task<List<T>> ListText<T>(SqlConnection connection, string sql, object parameters, CancellationToken ct)
            => (await connection.QueryAsync<T>(Text(sql, parameters, ct))).AsList();

        // ---------------------------------------------------------------- sync writes

        public async Task InsertSyncDataAsync(TransferDataModel model, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                // Same order and statements as the reference DataTransferDevAPIController.InsertSyncData.
                if (model.gradingGradingMapping is { Count: > 0 })
                {
                    await Exec(connection, transaction, @"
                    INSERT INTO dbo.ProjectGradingMapping_offline (Id, ProjectId, TemplateId, SectionId, GradingId, Value, IsDeleted, CreatedDttm, UpdatedDttm,IsModified)
                    VALUES (@Id, @ProjectId, @TemplateId, @SectionId, @GradingId, @Value, @IsDeleted, @CreatedDttm, @UpdatedDttm,@IsSync);",
                        model.gradingGradingMapping.Select(m => new
                        {
                            m.Id,
                            m.ProjectId,
                            m.TemplateId,
                            SectionId = m.SectionId.ToString().ToUpper(),
                            m.GradingId,
                            m.Value,
                            m.IsDeleted,
                            CreatedDttm = ClampSqlDate(m.CreatedDttm),
                            UpdatedDttm = ClampSqlDate(m.UpdatedDttm),
                            m.IsSync,
                        }), cancellationToken);
                }
                if (model.projectGradingConditionMappings is { Count: > 0 })
                {
                    await Exec(connection, transaction, @"INSERT INTO dbo.ProjectGradingConditionMapping_offline (Id, ProjectId, TemplateId, SectionId, GradingId, GradingConditionLabel, GradingConditionValue, IsDeleted, CreatedDttm, UpdatedDttm,IsModified)
                        VALUES (@Id, @ProjectId, @TemplateId, @SectionId, @GradingId, @GradingConditionLabel, @GradingConditionValue, @IsDeleted, @CreatedDttm, @UpdatedDttm,@IsSync)",
                        model.projectGradingConditionMappings.Select(m => new
                        {
                            m.Id,
                            m.ProjectId,
                            m.TemplateId,
                            SectionId = m.SectionId.ToString().ToUpper(),
                            m.GradingId,
                            m.GradingConditionLabel,
                            m.GradingConditionValue,
                            m.IsDeleted,
                            CreatedDttm = ClampSqlDate(m.CreatedDttm),
                            UpdatedDttm = ClampSqlDate(m.UpdatedDttm),
                            m.IsSync,
                        }), cancellationToken);
                }
                if (model.reportMapping is { Count: > 0 })
                {
                    await Exec(connection, transaction, @"INSERT INTO dbo.ProjectTemplateMapping_offline (Id, ProjectId, TemplateId, SectionId, Notes, IsDeleted, CreatedDttm, UpdatedDttm,IsModified) VALUES (@Id, @ProjectId, @TemplateId, @SectionId, @Notes, @IsDeleted, @CreatedDttm, @UpdatedDttm,@IsSync)",
                        model.reportMapping.Select(m => new
                        {
                            m.Id,
                            m.ProjectId,
                            m.TemplateId,
                            SectionId = m.SectionId.ToString().ToUpper(),
                            m.Notes,
                            m.IsDeleted,
                            CreatedDttm = ClampSqlDate(m.CreatedDttm),
                            UpdatedDttm = ClampSqlDate(m.UpdatedDttm),
                            m.IsSync
                        }), cancellationToken);
                }
                if (model.projectCardMappings is { Count: > 0 })
                {
                    await Exec(connection, transaction, @"INSERT INTO dbo.ProjectImageCards_offline (Id, ProjectId, TemplateId, SectionId, CardId, DescriptionId, AdditionalDescription, CurrentCondition, ImageId, IsDeleted, CreatedDttm, UpdateDttm,IsModified)
                    VALUES (@Id, @ProjectId, @TemplateId, @SectionId, @CardId, @DescriptionId, @AdditionalDescription, @CurrentCondition, @ImageId, @IsDeleted, @CreatedDttm, @UpdateDttm,@IsSync);",
                        model.projectCardMappings.Select(m => new
                        {
                            m.Id,
                            m.ProjectId,
                            m.TemplateId,
                            SectionId = m.SectionId.ToString().ToUpper(),
                            m.CardId,
                            m.DescriptionId,
                            m.AdditionalDescription,
                            m.CurrentCondition,
                            m.ImageId,
                            m.IsDeleted,
                            CreatedDttm = ClampSqlDate(m.CreatedDttm),
                            UpdateDttm = ClampSqlDate(m.UpdateDttm),
                            m.IsSync,
                        }), cancellationToken);
                }
                if (model.genericImageCard is { Count: > 0 })
                {
                    await Exec(connection, transaction, @"INSERT INTO dbo.GenericImageCard_offline (Id, ProjectId, TemplateId, SectionId, CardNumber, DescriptionId, AdditionalDescription, CurrentCondition, ImageId, IsDeleted, CreatedDttm, UpdateDttm,IsModified,IsActive,CardName)
                    VALUES (@Id, @ProjectId, @TemplateId, @SectionId, @CardNumber, @DescriptionId, @AdditionalDescription, @CurrentCondition, @ImageId, @IsDeleted, @CreatedDttm, @UpdateDttm,@IsSync,@IsActive,@CardName);",
                        model.genericImageCard.Select(m => new
                        {
                            m.Id,
                            m.ProjectId,
                            m.TemplateId,
                            SectionId = m.SectionId.ToString().ToUpper(),
                            m.CardNumber,
                            m.DescriptionId,
                            m.AdditionalDescription,
                            m.CurrentCondition,
                            m.ImageId,
                            m.IsDeleted,
                            CreatedDttm = ClampSqlDate(m.CreatedDttm),
                            UpdateDttm = ClampSqlDate(m.UpdatedDttm),
                            m.IsSync,
                            m.IsActive,
                            m.CardName
                        }), cancellationToken);
                }
                if (model.tankImageCards is { Count: > 0 })
                {
                    await Exec(connection, transaction, @"INSERT INTO dbo.TankImageCard_offline (Id, ProjectId, TemplateId, SectionId, CardNumber, CardName, DescriptionId, AdditionalDescription, CurrentCondition, ImageId, CreatedDttm, UpdatedDttm, IsActive,IsModified)
                    VALUES (@Id, @ProjectId, @TemplateId, @SectionId, @CardNumber, @CardName, @DescriptionId, @AdditionalDescription, @CurrentCondition, @ImageId, @CreatedDttm, @UpdatedDttm, @IsActive,@IsSync)",
                        model.tankImageCards.Select(m => new
                        {
                            m.Id,
                            m.ProjectId,
                            m.TemplateId,
                            SectionId = m.SectionId.ToString().ToUpper(),
                            m.CardNumber,
                            m.CardName,
                            m.DescriptionId,
                            m.AdditionalDescription,
                            m.CurrentCondition,
                            m.ImageId,
                            CreatedDttm = ClampSqlDate(m.CreatedDttm),
                            UpdatedDttm = ClampSqlDate(m.UpdatedDttm),
                            m.IsActive,
                            m.IsSync
                        }), cancellationToken);
                }
                if (model.images is { Count: > 0 })
                {
                    await Exec(connection, transaction, @"INSERT INTO upskill.Images_offline (Id, AssignmentId, FileId, InsertDate,ProjectId) VALUES (@Id, @AssignmentId, @FileId, @InsertDate,@ProjectId)",
                        model.images.Select(m => new
                        {
                            m.Id,
                            m.AssignmentId,
                            m.FileId,
                            InsertDate = ClampSqlDate(m.InsertDate),
                            m.ProjectId,
                        }), cancellationToken);
                }
                if (model.upskillImageData is { Count: > 0 })
                {
                    await Exec(connection, transaction, @"INSERT INTO upskill.ImageData_offline (Id, ImageId, Base64String, CreateDttm, UpdateDttm,ProjectId) VALUES (@Id, @ImageId, @Base64String, @CreateDttm, @UpdateDttm,@ProjectId)",
                        model.upskillImageData.Select(m => new
                        {
                            m.Id,
                            m.ImageId,
                            m.Base64String,
                            m.CreateDttm,
                            m.UpdateDttm,
                            m.ProjectId
                        }), cancellationToken);
                }
                if (model.handIProjectGradings is { Count: > 0 })
                {
                    await Exec(connection, transaction, "INSERT INTO dbo.HandIProjectGrading_offline (Id, ProjectID, GradingId, Value, IsActive, IsDeleted, CreatedBY, CreatedOn, ModifiedBY, ModifiedOn,IsModified) " +
                        "VALUES (@Id, @ProjectID, @GradingId, @Value, @IsActive, @IsDeleted, @CreatedBY, @CreatedOn, @ModifiedBY, @ModifiedOn,@IsSync)",
                        model.handIProjectGradings.Select(g => new
                        {
                            g.Id,
                            g.ProjectID,
                            g.GradingId,
                            g.Value,
                            g.IsActive,
                            g.IsDeleted,
                            g.CreatedBY,
                            g.CreatedOn,
                            g.ModifiedBY,
                            g.ModifiedOn,
                            g.IsSync
                        }), cancellationToken);
                }
                if (model.handIProjectGradingConditions is { Count: > 0 })
                {
                    await Exec(connection, transaction, "INSERT INTO HandIProjectGradingConditions_offline (Id, GradingID, ProjectId, ConditionLabel, Value, IsActive, IsDeleted, CreatedBY, CreatedOn, ModifiedBY, ModifiedOn,IsModified) " +
                        "VALUES (@Id, @GradingID, @ProjectId, @ConditionLabel, @Value, @IsActive, @IsDeleted, @CreatedBY, @CreatedOn, @ModifiedBY, @ModifiedOn,@IsSync)",
                        model.handIProjectGradingConditions.Select(c => new
                        {
                            c.Id,
                            c.GradingID,
                            c.ProjectId,
                            c.ConditionLabel,
                            c.Value,
                            c.IsActive,
                            c.IsDeleted,
                            c.CreatedBY,
                            c.CreatedOn,
                            c.ModifiedBY,
                            c.ModifiedOn,
                            c.IsSync
                        }), cancellationToken);
                }
                if (model.vesselTankMapping is { Count: > 0 })
                {
                    await Exec(connection, transaction, @"INSERT INTO dbo.Vessel_TankMapping_offline (Id, VesselType, Subheader, ImoNumber, TankTypeId, TankName, IsActive, CreatedDttm, UpdateDttm, IsDeleted,ProjectId)
                          VALUES (@Id, @VesselType,@Subheader, @ImoNumber, @TankTypeId, @TankName, @IsActive, @CreatedDttm, @UpdateDttm, @IsDeleted,@ProjectId)",
                        model.vesselTankMapping.Select(m => new
                        {
                            m.Id,
                            m.VesselType,
                            m.ImoNumber,
                            m.TankTypeId,
                            m.TankName,
                            m.IsActive,
                            m.CreatedDttm,
                            m.UpdateDttm,
                            m.IsDeleted,
                            m.ProjectId,
                            m.Subheader
                        }), cancellationToken);
                }
                if (model.HIReport != null)
                {
                    await Exec(connection, transaction, @"
                    INSERT INTO dbo.H_IReportTemplate_offline (
                        Id, ProjectId, CompanyName, CertificateNumber, ExpiryDate, ThicknessMeasurementA,
                        ThicknessMeasurementB, ThicknessMeasurementC, AdditionalSurveyorComments, MaintenanceExecuted,
                        Accommodation, PhotoGraphs, OperationalTestLocation, OperationalTestStart, OperationalTestFinish,
                        OptionalTestSatisFactory, AssociatedReportNumber, AssociatedReportDate, Remarks, LeadSurveyorName,
                        LeadSurveyorNumber, SurveyorName, SurveyorNumber, LastVisitDate, IndividualA, IndividualB,
                        IndividualC, IndividualD, IndividualE, IndividualF, SubstantialCorrosionRemaining,
                        StatutoryDeficiencies, FoundSatisfactory, UnderRemarks, ScouringOrPitting,
                        BottomPlattingScouringOrPitting, IsActive, IsDeleted, CreatedDttm, UpdatedDttm, OthersDescription)
                    VALUES (
                        @Id, @ProjectId, @CompanyName, @CertificateNumber, @ExpiryDate, @ThicknessMeasurementA,
                        @ThicknessMeasurementB, @ThicknessMeasurementC, @AdditionalSurveyorComments, @MaintenanceExecuted,
                        @Accommodation, @PhotoGraphs, @OperationalTestLocation, @OperationalTestStart, @OperationalTestFinish,
                        @OptionalTestSatisFactory, @AssociatedReportNumber, @AssociatedReportDate, @Remarks, @LeadSurveyorName,
                        @LeadSurveyorNumber, @SurveyorName, @SurveyorNumber, @LastVisitDate, @IndividualA, @IndividualB,
                        @IndividualC, @IndividualD, @IndividualE, @IndividualF, @SubstantialCorrosionRemaining,
                        @StatutoryDeficiencies, @FoundSatisfactory, @UnderRemarks, @ScouringOrPitting,
                        @BottomPlattingScouringOrPitting, @IsActive, @IsDeleted, @CreatedDttm, @UpdatedDttm, @OthersDescription);",
                        model.HIReport, cancellationToken);
                }

                await connection.ExecuteAsync(Sp(SpMoveToHistoryTable, new { projectId = model.ProjectId }, cancellationToken, transaction));

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        private static Task<int> Exec(SqlConnection connection, IDbTransaction transaction, string sql, object parameters, CancellationToken ct)
            => connection.ExecuteAsync(Text(sql, parameters, ct, transaction));

        public async Task MoveToOnlineTableAsync(int projectId, int keep, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await connection.ExecuteAsync(Sp(SpMoveToOnlineTable, new { projectId, Keep = keep }, cancellationToken, transaction));
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        public async Task ReleaseSyncDataAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            await connection.ExecuteAsync(Sp(SpReleaseSyncData, new { projectId }, cancellationToken));
        }

        public async Task ReleaseErrorSyncDataAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            await connection.ExecuteAsync(Sp(SpReleaseErrorSyncData, new { ProjectId = projectId }, cancellationToken));
        }

        public async Task<List<DropdownList>> GetAllDropDownProjectsAsync(string? userId, string? userName, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            var results = await connection.QueryAsync<DropdownList>(Sp(SpGetProject, new { id = userId, username = userName }, cancellationToken));
            return results.ToList();
        }

        // Reference GradingValidateDateTime & co.: keep DateTime within SQL Server's datetime range.
        private static DateTime ClampSqlDate(DateTime value)
        {
            if (value < SqlDateTime.MinValue.Value) return SqlDateTime.MinValue.Value;
            if (value > SqlDateTime.MaxValue.Value) return SqlDateTime.MaxValue.Value;
            return value;
        }
    }
}
