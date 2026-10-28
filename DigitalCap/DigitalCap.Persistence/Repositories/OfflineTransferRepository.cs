using Dapper;
using DigitalCap.Core.Constants;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.DataTransfer;
using DigitalCap.Core.Models.ImageDescription;
using DigitalCap.Core.Models.ReportConfig;
using DigitalCap.Core.Models.Survey;
using DigitalCap.Core.Models.Tank;
using DigitalCap.Core.Services;
using DigitalCap.Core.ViewModels;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace DigitalCap.Persistence.Repositories
{
    /// <summary>
    /// Local SQLCipher side of download/sync, ported from digital-cap-offline TransferDataOfflineRepository
    /// (inserts, release, keep, backup) and SyncTransferDataOfflineRepository (dirty-row reads).
    /// Unlike the reference, every multi-statement operation runs in one transaction and errors are never swallowed.
    /// </summary>
    public class OfflineTransferRepository : IOfflineTransferRepository
    {
        private const string BackupSuffix = "_backup";

        /// <summary>
        /// Every per-project table the download writes (except ProjectGrades, which has no backup table), with the
        /// predicate that selects the project's rows. {0} is the table-name suffix ("" or "_backup") so the same
        /// predicate scopes the live tables and the backup tables. Ordered child-first so deletes can use parents.
        /// Predicates mirror SQLiteQueries.ReleaseProject.Release / MoveToBackUp.
        /// </summary>
        private static readonly (string Table, string Predicate)[] ProjectTables =
        {
            ("ProjectSectionDescriptionMapping", "SectionId IN (SELECT SectionMappingId FROM ProjectSectionMapping{0} WHERE ProjectId=@ProjectId)"),
            ("ProjectSubSectionDescriptionMapping", "SubSectionId IN (SELECT pss.Id FROM ProjectSubSection{0} pss INNER JOIN ProjectSectionMapping{0} psm ON psm.SectionMappingId = pss.ProjectSectionId WHERE psm.ProjectId=@ProjectId)"),
            ("ProjectSectionGradingMapping", "SectionId IN (SELECT SectionMappingId FROM ProjectSectionMapping{0} WHERE ProjectId=@ProjectId UNION ALL SELECT Id FROM ProjectSubSection{0} WHERE ProjectSectionId IN (SELECT SectionMappingId FROM ProjectSectionMapping{0} WHERE ProjectId=@ProjectId))"),
            ("ProjectSubSection", "ProjectSectionId IN (SELECT SectionMappingId FROM ProjectSectionMapping{0} WHERE ProjectId=@ProjectId)"),
            ("ProjectSectionMapping", "ProjectId=@ProjectId"),
            ("ProjectTankSectionMapping", "ProjectId=@ProjectId"),
            ("Images", "ProjectId=@ProjectId"),
            ("ImageData", "ProjectId=@ProjectId"),
            ("ReportPart", "ProjectId=@ProjectId"),
            ("ProjectGradingMapping", "ProjectId=@ProjectId"),
            ("H_IReportTemplate", "ProjectId=@ProjectId"),
            ("Comments", "ProjectId=@ProjectId"),
            ("ProjectGradingConditionMapping", "ProjectId=@ProjectId"),
            ("ProjectTemplateMapping", "ProjectId=@ProjectId"),
            ("Activities", "ProjectId=@ProjectId"),
            ("ProjectImageCards", "ProjectId=@ProjectId"),
            ("GenericImageCard", "ProjectId=@ProjectId"),
            ("TankImageCard", "ProjectId=@ProjectId"),
            ("Grading", "ProjectId=@ProjectId"),
            ("ImageDescriptions", "ProjectId=@ProjectId"),
            ("Vessel_GradingMapping", "ProjectId=@ProjectId"),
            ("Vessel_TankMapping", "ImoNumber=@Imo and ProjectId=@ProjectId"),
            ("Projects", "ID=@ProjectId"),
            ("ReportSurveyDetails", "ProjectId=@ProjectId"),
            ("ReportVesselMainData", "ProjectId=@ProjectId"),
            ("ReportStatutoryCertificates", "ProjectId=@ProjectId"),
            ("HandIProjectGradingConditions", "ProjectId=@ProjectId"),
            ("HandIProjectGrading", "ProjectId=@ProjectId"),
        };

        private const string DeleteProjectGrades = "DELETE FROM ProjectGrades WHERE ProjectId=@ProjectId";
        // Workflow tasks are a read-only snapshot (no backup table): replaced on every download, removed on release.
        private const string DeleteProjectTasks = "DELETE FROM ProjectTaskStatus WHERE ProjectId=@ProjectId";

        public const string DownloadedActivityText = "Project Downloaded in Offline mode";
        public const string KeptActivityText = "Project Synched to Online and Kept the Offline Project";

        private readonly Func<SqliteConnection> _connectionFactory;

        public OfflineTransferRepository() : this(DefaultConnectionFactory) { }

        /// <summary>For tests: supply the connection factory (must return an unopened connection).</summary>
        public OfflineTransferRepository(Func<SqliteConnection> connectionFactory)
        {
            _connectionFactory = connectionFactory;
        }

        private static SqliteConnection DefaultConnectionFactory()
        {
            var dbPath = new OneDrivePathService().GetDatabasePath();
            return new SqliteConnection($"Data Source={dbPath};Password={AppKeyService.GetDecryptedKey()};");
        }

        private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
        {
            var connection = _connectionFactory();
            await connection.OpenAsync(ct);
            return connection;
        }

        // ================================================================ simple reads

        public async Task<bool> ProjectExistsAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            return await connection.ExecuteScalarAsync<long>(
                new CommandDefinition("SELECT COUNT(1) FROM Projects WHERE ID=@ProjectId", new { ProjectId = projectId }, cancellationToken: cancellationToken)) > 0;
        }

        public async Task<List<int>> GetProjectIdsAsync(CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            return (await connection.QueryAsync<int>(new CommandDefinition(SQLiteQueries.SurveyReport.GetAllProject, cancellationToken: cancellationToken))).AsList();
        }

        public async Task<PendingChanges> GetPendingChangesAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            var sections = await QueryDirtySections(connection, null, projectId, cancellationToken);

            var imo = await GetImo(connection, null, projectId, fromBackup: false);
            var p = new { ProjectId = projectId, IMO = imo };
            long tankRows = 0;
            foreach (var sql in new[]
                     {
                         SQLiteQueries.SyncDataTransfer.GetVessel_TankMappingByProject,
                         SQLiteQueries.SyncDataTransfer.GetH_IReportTemplate,
                         SQLiteQueries.SyncDataTransfer.GetHandIProjectGradings,
                         SQLiteQueries.SyncDataTransfer.GetHandIProjectGradingConditions,
                     })
            {
                tankRows += await connection.ExecuteScalarAsync<long>(
                    new CommandDefinition($"SELECT COUNT(1) FROM ({sql.TrimEnd(';', ' ')})", p, cancellationToken: cancellationToken));
            }

            return new PendingChanges
            {
                HasChanges = sections.Count > 0 || tankRows > 0,
                SectionCount = sections.Count
            };
        }

        // ================================================================ download write

        public async Task SaveDownloadedProjectAsync(int projectId, TransferDataModel data, bool replaceExisting, string activityUser, CancellationToken cancellationToken = default)
        {
            if (data?.Project == null)
                throw new ArgumentException("Downloaded data has no project.", nameof(data));

            await using var connection = await OpenAsync(cancellationToken);
            await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                if (replaceExisting)
                {
                    var imo = await GetImo(connection, tx, projectId, fromBackup: false);
                    var scope = new { ProjectId = projectId, Imo = imo };
                    EnsureBackupTablesExist(connection, tx);
                    // Stale backup rows of this project (e.g. from an interrupted run) would be duplicated otherwise.
                    DeleteScoped(connection, tx, BackupSuffix, scope);
                    CopyScoped(connection, tx, "", BackupSuffix, scope);
                    DeleteScoped(connection, tx, "", scope);
                    connection.Execute(DeleteProjectGrades, new { ProjectId = projectId }, tx);
                }
                else if (await connection.ExecuteScalarAsync<long>("SELECT COUNT(1) FROM Projects WHERE ID=@ProjectId", new { ProjectId = projectId }, tx) > 0)
                {
                    throw new InvalidOperationException($"Project {projectId} already exists locally.");
                }

                cancellationToken.ThrowIfCancellationRequested();
                InsertProjectData(connection, tx, data);
                cancellationToken.ThrowIfCancellationRequested();
                InsertSectionData(connection, tx, data);
                if (data.ProjectTasks != null) ReplaceProjectTasks(connection, tx, projectId, data.ProjectTasks);
                if (data.UserSecurity != null) ReplaceUserSecurity(connection, tx, data.UserSecurity);
                if (data.UserSecurity != null && !string.IsNullOrWhiteSpace(data.UserSecurity.UserName))
                {
                    var emailId = data.UserSecurity.UserName.Trim();
                    connection.Execute("DELETE FROM LogInData WHERE lower(EmailId) = lower(@EmailId);", new { EmailId = emailId }, tx);
                    connection.Execute("INSERT INTO LogInData (Id, FirstName, LastName, ProviderKey, EmailId) VALUES (@Id, NULL, NULL, NULL, @EmailId);",
                        new { Id = data.UserSecurity.AspNetUserId, EmailId = emailId }, tx);
                }

                InsertActivity(connection, tx, projectId, activityUser, DownloadedActivityText);

                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        public async Task RestoreBackupAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                var liveScope = new { ProjectId = projectId, Imo = await GetImo(connection, tx, projectId, fromBackup: false) };
                var backupScope = new { ProjectId = projectId, Imo = await GetImo(connection, tx, projectId, fromBackup: true) };

                DeleteScoped(connection, tx, "", liveScope);
                CopyScoped(connection, tx, BackupSuffix, "", backupScope);
                DeleteScoped(connection, tx, BackupSuffix, backupScope);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        public async Task ClearBackupAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                var scope = new { ProjectId = projectId, Imo = await GetImo(connection, tx, projectId, fromBackup: true) };
                DeleteScoped(connection, tx, BackupSuffix, scope);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        // ================================================================ release / keep

        public async Task ReleaseAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                // IMO is read inside the same transaction (the reference looked it up on a separate connection).
                var scope = new { ProjectId = projectId, Imo = await GetImo(connection, tx, projectId, fromBackup: false) };
                DeleteScoped(connection, tx, "", scope);
                connection.Execute(DeleteProjectGrades, new { ProjectId = projectId }, tx);
                connection.Execute(DeleteProjectTasks, new { ProjectId = projectId }, tx);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        public async Task KeepDownloadedAsync(int projectId, string activityUser, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var tx = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                var imo = await GetImo(connection, tx, projectId, fromBackup: false);
                foreach (var statement in SQLiteQueries.ReleaseProject.KeepDownloaded)
                {
                    if (statement.Contains("Vessel_TankMapping"))
                        connection.Execute(statement, new { Imo = imo, ProjectId = projectId }, tx);
                    else
                        connection.Execute(statement, new { ProjectId = projectId }, tx);
                }
                InsertActivity(connection, tx, projectId, activityUser, KeptActivityText);
                await tx.CommitAsync(cancellationToken);
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                throw;
            }
        }

        // ================================================================ upload reads

        public async Task<TransferDataModel> GetTankSyncDataAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            var imo = await GetImo(connection, null, projectId, fromBackup: false);
            var model = new TransferDataModel();
            if (imo == null)
                return model; // project not local: nothing to upload (reference would have thrown on project.IMO)

            // Reference GetTankData: model.ProjectId is left 0.
            model.vesselTankMapping = await Read<VesselTankMapping>(connection, SQLiteQueries.SyncDataTransfer.GetVessel_TankMappingByProject, new { IMO = imo, ProjectId = projectId }, cancellationToken);
            model.HIReport = (await Read<HIReportTemplate>(connection, SQLiteQueries.SyncDataTransfer.GetH_IReportTemplate, new { ProjectId = projectId }, cancellationToken)).FirstOrDefault()!; // null when unchanged
            model.handIProjectGradings = await Read<HandIProjectGrading>(connection, SQLiteQueries.SyncDataTransfer.GetHandIProjectGradings, new { ProjectId = projectId }, cancellationToken);
            model.handIProjectGradingConditions = await Read<HandIProjectGradingConditions>(connection, SQLiteQueries.SyncDataTransfer.GetHandIProjectGradingConditions, new { ProjectId = projectId }, cancellationToken);
            return model;
        }

        public async Task<List<DirtySection>> GetDirtySectionsAsync(int projectId, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            return await QueryDirtySections(connection, null, projectId, cancellationToken);
        }

        public async Task<TransferDataModel> GetSectionSyncDataAsync(int projectId, DirtySection section, CancellationToken cancellationToken = default)
        {
            await using var connection = await OpenAsync(cancellationToken);
            // SectionId is passed exactly as stored, so the text comparison in the queries matches.
            var p = new { ProjectId = projectId, TemplateId = section.TemplateId, SectionId = section.SectionId };

            return new TransferDataModel
            {
                ProjectId = projectId,
                gradingGradingMapping = await Read<ProjectGradingMapping>(connection, SQLiteQueries.SyncDataTransfer.GetProjectGradingByTemplateId, p, cancellationToken),
                projectGradingConditionMappings = await Read<ProjectGradingConditionMapping>(connection, SQLiteQueries.SyncDataTransfer.GetProjectGradingConditionMappingByTemplateId, p, cancellationToken),
                reportMapping = await Read<ProjectReportMapping>(connection, SQLiteQueries.SyncDataTransfer.GetProjectTemplateMappingByTemplateId, p, cancellationToken),
                projectCardMappings = await Read<ProjectCardMapping>(connection, SQLiteQueries.ProjectImageCards.GetProjectImageCardsByTemplateId, p, cancellationToken),
                genericImageCard = await Read<GenericImageCard>(connection, SQLiteQueries.GenericImageCard.GetGenericImageCardsByTemplateId, p, cancellationToken),
                tankImageCards = await Read<TankImageCard>(connection, SQLiteQueries.SyncDataTransfer.GetTankImageCardByTemplateId, p, cancellationToken),
                images = await Read<Images>(connection, SQLiteQueries.ProjectImageCards.GetImages, p, cancellationToken),
                upskillImageData = await Read<UpskillImageData>(connection, SQLiteQueries.ProjectImageCards.GetImageData, p, cancellationToken),
            };
        }

        private static async Task<List<DirtySection>> QueryDirtySections(SqliteConnection connection, IDbTransaction? tx, int projectId, CancellationToken ct)
        {
            // Reference OfflineController.GetAllSections -> SQLiteQueries.ReportView.GetAllTemplateSections (UNION of dirty rows).
            var rows = await connection.QueryAsync(new CommandDefinition(SQLiteQueries.ReportView.GetAllTemplateSections, new { ProjectId = projectId }, tx, cancellationToken: ct));
            var result = new List<DirtySection>();
            foreach (IDictionary<string, object> row in rows)
            {
                var sectionId = row["SectionId"]?.ToString();
                if (string.IsNullOrEmpty(sectionId))
                    continue;
                result.Add(new DirtySection { TemplateId = Convert.ToInt32(row["TemplateId"], CultureInfo.InvariantCulture), SectionId = sectionId });
            }
            return result;
        }

        // ================================================================ inserts (exact reference statements)

        private static void InsertProjectData(SqliteConnection c, IDbTransaction tx, TransferDataModel m)
        {
            // Order of the reference DataTransferAPIController.InsertDataToSQLiteByProjectId.
            InsertProject(c, tx, m.Project);
            if (m.comments is { Count: > 0 }) InsertComments(c, tx, m.comments);
            if (m.HIReport != null) InsertIReportTemplate(c, tx, m.HIReport);
            if (m.handIProjectGradings is { Count: > 0 }) InsertHandIProjectGradings(c, tx, m.handIProjectGradings);
            if (m.handIProjectGradingConditions is { Count: > 0 }) InsertHandIProjectGradingConditions(c, tx, m.handIProjectGradingConditions);
            if (m.Activities is { Count: > 0 }) InsertActivities(c, tx, m.Activities);
            if (m.VesselMainData is { Count: > 0 }) InsertVesselMainData(c, tx, m.VesselMainData);
            if (m.SurveyDetails is { Count: > 0 }) InsertReportSurveyDetails(c, tx, m.SurveyDetails);
            if (m.StatutoryCertificates is { Count: > 0 }) InsertReportStatutoryCertificates(c, tx, m.StatutoryCertificates);
            if (m.ReportPart is { Count: > 0 }) InsertReportParts(c, tx, m.ReportPart);
            if (m.ProjectTankSection is { Count: > 0 }) InsertProjectTankSectionMappings(c, tx, m.ProjectTankSection);
            if (m.ProjectSectionMapping is { Count: > 0 }) InsertProjectSectionMappings(c, tx, m.ProjectSectionMapping);
            if (m.ProjectSubSection is { Count: > 0 }) InsertProjectSubSections(c, tx, m.ProjectSubSection);
            if (m.ProjectSectionGrading is { Count: > 0 }) InsertProjectSectionGradingMappings(c, tx, m.ProjectSectionGrading);
            if (m.ProjectSectionDescription is { Count: > 0 }) InsertProjectSectionDescriptionMappings(c, tx, m.ProjectSectionDescription);
            if (m.ProjectSubSectionDescription is { Count: > 0 }) InsertProjectSubSectionDescriptionMappings(c, tx, m.ProjectSubSectionDescription);
            if (m.H_I_Grading is { Count: > 0 }) InsertHIGrading(c, tx, m.H_I_Grading);
            if (m.projectGrades is { Count: > 0 }) InsertProjectGrades(c, tx, m.projectGrades);
            if (m.gradingData is { Count: > 0 }) InsertGradingData(c, tx, m.gradingData);
            if (m.imageDescriptions is { Count: > 0 }) InsertImageDescriptionsData(c, tx, m.imageDescriptions);
            if (m.vesselTankMapping is { Count: > 0 }) InsertVesselTankMapping(c, tx, m.vesselTankMapping);
            if (m.gradingMapping is { Count: > 0 }) InsertVesselGradingMappings(c, tx, m.gradingMapping);
            if (m.VesselTypeTankMapping is { Count: > 0 }) InsertVesselTypeTankMapping(c, tx, m.VesselTypeTankMapping);
        }

        private static void InsertSectionData(SqliteConnection c, IDbTransaction tx, TransferDataModel m)
        {
            // Order of the reference InsertDataToSQLiteByTemplateId. The service merges all sections into one model.
            if (m.gradingGradingMapping is { Count: > 0 }) InsertProjectGradingMapping(c, tx, m.gradingGradingMapping);
            if (m.projectGradingConditionMappings is { Count: > 0 }) InsertProjectGradingConditionMapping(c, tx, m.projectGradingConditionMappings);
            if (m.reportMapping is { Count: > 0 }) InsertProjectTemplateMapping(c, tx, m.reportMapping);
            if (m.projectCardMappings is { Count: > 0 }) InsertProjectCardMapping(c, tx, m.projectCardMappings);
            if (m.genericImageCard is { Count: > 0 }) InsertGenericCardMapping(c, tx, m.genericImageCard);
            if (m.tankImageCards is { Count: > 0 }) InsertTankImageCard(c, tx, m.tankImageCards);
            if (m.images is { Count: > 0 }) InsertImages(c, tx, m.images);
            if (m.upskillImageData is { Count: > 0 }) InsertImageData(c, tx, m.upskillImageData);
        }

        private static void InsertProject(SqliteConnection c, IDbTransaction tx, ProjectDataTransfer project)
        {
            c.Execute(@"
    INSERT INTO Projects (
        ID, PIDNumber, CapRegion, CapScope, DrydockLocation, DryDockStart, DryDockEnd, ExpenseMarkup,
        SurveyDayRateClient, SurveyDayRateAbs, SedRate1, SedRate2, AgreementNumber, CapContractValue,
        AgreementOwner, AgreementDate, AgreementSignedDate, SedEvaluationGrade, CertificateGrade,
        ProjectComments, CompanyName, CompanyAddress, CompanyBillingEmail, CompanyBillingSystemUrl,
        BillSameAsCapClient, BillToCompanyName, BillToCompanyAddress, BillToCompanyEmail, BillToCompanyBillingUrl,
        PocName, PocEmail, PocPhone, IMO, VesselName, WCN, ClassSociety, AbsClassID, SisterVesselImoNumber,
        SisterVesselName, Builder, MonthBuilt, YearBuilt, CreatedDate, ModifiedDate, LastModifiedBy,
        ProjectStatus, ProjectPriority, ShipType, HullNumber, LengthOverall, SisterVessel, SpecialHull,
        FatigueAnalysis, CapType, ProposalRev, CRMOpportunityNumber, Name, PotentialDrydockDate, SurveyFirstVisit,
        SurveyLastVisit, BillToWCN, LeadSurveyor, LeadSurveyorEID, SecondSurveyor, SecondSurveyorEID,
        ThirdSurveyor, ThirdSurveyorEID, FourthSurveyor, FourthSurveyorEID, AdditionalSurveyors, CapScopeOther,
        ClassSocietyOther, ClientProfileId, BillToClientProfileId, CreatedBy, StatusChangedBy, StatusChangedDate,
        PercentComplete, VesselType, IsDefaultTank, CopyingVesselID, TankVesselType, TankVesselIMO)
    VALUES (
        @ID, @PIDNumber, @CapRegion, @CapScope, @DrydockLocation, @DryDockStart, @DryDockEnd, @ExpenseMarkup,
        @SurveyDayRateClient, @SurveyDayRateAbs, @SedRate1, @SedRate2, @AgreementNumber, @CapContractValue,
        @AgreementOwner, @AgreementDate, @AgreementSignedDate, @SedEvaluationGrade, @CertificateGrade,
        @ProjectComments, @CompanyName, @CompanyAddress, @CompanyBillingEmail, @CompanyBillingSystemUrl,
        @BillSameAsCapClient, @BillToCompanyName, @BillToCompanyAddress, @BillToCompanyEmail, @BillToCompanyBillingUrl,
        @PocName, @PocEmail, @PocPhone, @IMO, @VesselName, @WCN, @ClassSociety, @AbsClassID, @SisterVesselImoNumber,
        @SisterVesselName, @Builder, @MonthBuilt, @YearBuilt, @CreatedDate, @ModifiedDate, @LastModifiedBy,
        @ProjectStatus, @ProjectPriority, @ShipType, @HullNumber, @LengthOverall, @SisterVessel, @SpecialHull,
        @FatigueAnalysis, @CapType, @ProposalRev, @CRMOpportunityNumber, @Name, @PotentialDrydockDate, @SurveyFirstVisit,
        @SurveyLastVisit, @BillToWCN, @LeadSurveyor, @LeadSurveyorEID, @SecondSurveyor, @SecondSurveyorEID,
        @ThirdSurveyor, @ThirdSurveyorEID, @FourthSurveyor, @FourthSurveyorEID, @AdditionalSurveyors, @CapScopeOther,
        @ClassSocietyOther, @ClientProfileId, @BillToClientProfileId, @CreatedBy, @StatusChangedBy, @StatusChangedDate,
        @PercentComplete, @VesselType, @IsDefaultTank, @CopyingVesselID, @TankVesselType, @TankVesselIMO);
", project, tx);
        }

        private static void InsertComments(SqliteConnection c, IDbTransaction tx, List<Comment> comments)
        {
            c.Execute(
                "INSERT INTO Comments (Id, ProjectId, ProjectTaskId, Comment, CreatedBy, LastModifiedDate) " +
                "VALUES (@Id, @ProjectId, @ProjectTaskId, @CommentText, @CreatedBy, @LastModifiedDate)",
                comments.Select(x => new { x.Id, x.ProjectId, x.ProjectTaskId, x.CommentText, x.CreatedBy, x.LastModifiedDate }), tx);
        }

        private static void InsertIReportTemplate(SqliteConnection c, IDbTransaction tx, HIReportTemplate reportTemplate)
        {
            c.Execute(@"
            INSERT INTO H_IReportTemplate (
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
                @BottomPlattingScouringOrPitting, @IsActive, @IsDeleted, @CreatedDttm, @UpdatedDttm, @OthersDescription);
        ", reportTemplate, tx);
        }

        private static void InsertHandIProjectGradings(SqliteConnection c, IDbTransaction tx, List<HandIProjectGrading> list)
        {
            c.Execute(
                "INSERT INTO HandIProjectGrading (Id, ProjectID, GradingId, Value, IsActive, IsDeleted, CreatedBY, CreatedOn, ModifiedBY, ModifiedOn) " +
                "VALUES (@Id, @ProjectID, @GradingId, @Value, @IsActive, @IsDeleted, @CreatedBY, @CreatedOn, @ModifiedBY, @ModifiedOn)",
                list.Select(g => new { g.Id, g.ProjectID, g.GradingId, g.Value, g.IsActive, g.IsDeleted, g.CreatedBY, g.CreatedOn, g.ModifiedBY, g.ModifiedOn }), tx);
        }

        private static void InsertHandIProjectGradingConditions(SqliteConnection c, IDbTransaction tx, List<HandIProjectGradingConditions> list)
        {
            c.Execute(
                "INSERT INTO HandIProjectGradingConditions (Id, GradingID, ProjectId, ConditionLabel, Value, IsActive, IsDeleted, CreatedBY, CreatedOn, ModifiedBY, ModifiedOn) " +
                "VALUES (@Id, @GradingID, @ProjectId, @ConditionLabel, @Value, @IsActive, @IsDeleted, @CreatedBY, @CreatedOn, @ModifiedBY, @ModifiedOn)",
                list.Select(x => new { x.Id, x.GradingID, x.ProjectId, x.ConditionLabel, x.Value, x.IsActive, x.IsDeleted, x.CreatedBY, x.CreatedOn, x.ModifiedBY, x.ModifiedOn }), tx);
        }

        private static void InsertActivities(SqliteConnection c, IDbTransaction tx, List<global::CapActivity> list)
        {
            foreach (var a in list)
            {
                // Reference skips activities whose ID already exists locally.
                var exists = c.ExecuteScalar<long>("SELECT COUNT(1) FROM [Activities] WHERE ID = @Id", new { Id = a.ID }, tx);
                if (exists != 0)
                    continue;
                c.Execute(@"INSERT INTO [Activities] (ID,LinkText, LinkHref, Text, Icon, IsDeleted, IsRead, ReadBy, ReadDttm, CreateBy, CreateDttm, ProjectId)
                        VALUES (@Id,@LinkText, @LinkHref, @Text, @Icon, @IsDeleted, @IsRead, @ReadBy, @ReadDttm, @CreateBy, @CreateDttm, @ProjectId)",
                    new { Id = a.ID, a.LinkText, a.LinkHref, a.Text, a.Icon, a.IsDeleted, a.IsRead, a.ReadBy, a.ReadDttm, a.CreateBy, a.CreateDttm, a.ProjectId }, tx);
            }
        }

        /// <summary>
        /// Stores the project's CAP2 tasks (from [CAP].[Read_Cap2Tasks_ByProjectId]) in ProjectTaskStatus so
        /// SQLiteQueries.Task.ReadCap2TasksByProjectId can rebuild them offline (Category/Name/Type come from Tasks).
        /// Rows without a TaskId cannot be joined to Tasks and are skipped.
        /// </summary>
        private static void ReplaceProjectTasks(SqliteConnection c, IDbTransaction tx, int projectId, List<SingleTask> tasks)
        {
            c.Execute(DeleteProjectTasks, new { ProjectId = projectId }, tx);
            var rows = tasks.Where(t => t.ID.HasValue && t.TaskId.HasValue).Select(t => new
            {
                ID = t.ID!.Value,
                ProjectId = projectId,
                TaskId = t.TaskId!.Value,
                t.StatusId,
                t.Value,
                t.Notes,
                t.LastModifiedDate,
                LastModifiedBy = t.LastModifiedBy ?? "SYSTEM",
                t.StatusDate,
                t.DueDate,
                t.AssignedTo,
                t.AssignedDate,
                t.SurveyDays
            }).ToList();
            if (rows.Count == 0)
                return;
            c.Execute(@"
                INSERT INTO ProjectTaskStatus (ID, ProjectId, TaskId, StatusId, Value, Notes, LastModifiedDate, LastModifiedBy, StatusDate, DueDate, AssignedTo, AssignedDate, SurveyDays)
                VALUES (@ID, @ProjectId, @TaskId, @StatusId, @Value, @Notes, @LastModifiedDate, @LastModifiedBy, @StatusDate, @DueDate, @AssignedTo, @AssignedDate, @SurveyDays)", rows, tx);
        }

        /// <summary>
        /// Replaces the stored role-page permission matrix and this user's roles/claims (other users who downloaded
        /// on this computer keep theirs).
        /// </summary>
        private static void ReplaceUserSecurity(SqliteConnection c, IDbTransaction tx, OfflineUserSecurity security)
        {
            if (string.IsNullOrWhiteSpace(security.UserName))
                throw new ArgumentException("The downloaded permissions have no user name.", nameof(security));

            var user = new { UserName = security.UserName.Trim() };
            c.Execute("DELETE FROM OfflineUserRoles WHERE UserName = @UserName", user, tx);
            c.Execute("DELETE FROM OfflineUserClaims WHERE UserName = @UserName", user, tx);
            c.Execute("DELETE FROM OfflineUserSecurity WHERE UserName = @UserName", user, tx);
            c.Execute("INSERT INTO OfflineUserSecurity (UserName, AspNetUserId, StoredAt) VALUES (@UserName, @AspNetUserId, @StoredAt)",
                new { user.UserName, security.AspNetUserId, StoredAt = DateTime.UtcNow }, tx);
            c.Execute("INSERT INTO OfflineUserRoles (UserName, RoleName) VALUES (@UserName, @RoleName)",
                security.Roles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(r => new { user.UserName, RoleName = r }), tx);
            c.Execute("INSERT INTO OfflineUserClaims (UserName, ClaimType) VALUES (@UserName, @ClaimType)",
                security.Claims.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal)
                    .Select(x => new { user.UserName, ClaimType = x }), tx);

            c.Execute("DELETE FROM OfflineRolePermissions", transaction: tx);
            c.Execute("INSERT INTO OfflineRolePermissions (RoleName, PermissionName, [Read], [Edit], [Delete]) VALUES (@RoleName, @PermissionName, @Read, @Edit, @Delete)",
                security.RolePermissions.Where(p => !string.IsNullOrWhiteSpace(p.RoleName) && !string.IsNullOrWhiteSpace(p.PermissionName)), tx);
        }

        private static void InsertActivity(SqliteConnection c, IDbTransaction tx, int projectId, string user, string text)
        {
            c.Execute(SQLiteQueries.DataTransfer.InsertActivities, new { ProjectId = projectId, CreateBy = user, Text = text }, tx);
        }

        private static void InsertVesselMainData(SqliteConnection c, IDbTransaction tx, List<GetReportVesselMainData> list)
        {
            c.Execute(
                "INSERT INTO ReportVesselMainData  (ProjectId, ReportNo, PortDate, ActualReportStartDate, VesselName, " +
                "VesselID, ImoNumber, VesselType, YearBuilt, BuiltBy, HullNo, FlagName, Homeport, OfficalNumber, CallSign, " +
                "PreviousName, OwnerName, Manager, Length, Breadth, Depth, Draft, Deadweight, GrossTons, NetTons, PropulsionType, " +
                "KW, ShaftRPM, PropulsionManufacturer, GearManufacturer, Classed, ClassNo, ClassNotation, Machinery, Other, " +
                "Environment, ReportPort, FirstVisitDate, LastVisitDate, SelectedPrefix) " +
                "VALUES (@ProjectId, @ReportNo, @PortDate, @ActualReportStartDate, @VesselName, @VesselID, @ImoNumber, " +
                "@VesselType, @YearBuilt, @BuiltBy, @HullNo, @FlagName, @Homeport, @OfficalNumber, @CallSign, @PreviousName, " +
                "@OwnerName, @Manager, @Length, @Breadth, @Depth, @Draft, @Deadweight, @GrossTons, @NetTons, @PropulsionType, " +
                "@KW, @ShaftRPM, @PropulsionManufacturer, @GearManufacturer, @Classed, @ClassNo, @ClassNotation, @Machinery, " +
                "@Other, @Environment, @ReportPort, @FirstVisitDate, @LastVisitDate, @SelectedPrefix)",
                list, tx);
        }

        private static void InsertReportSurveyDetails(SqliteConnection c, IDbTransaction tx, List<ReportSurveyDetails> list)
        {
            c.Execute(
                "INSERT INTO ReportSurveyDetails  (ProjectId, ImoNum, ClassSurveyDate, DryDockDate, " +
                "SpecialContinuousMachineryDate, BoilerDate, TailshaftDate, SpecialContinuousHull1, " +
                "SpecialContinuousHull2, SpecialContinuousHull3, SpecialContinuousHull4, SpecialContinuousHull5, " +
                "SpecialContinuousHull6, SpecialContinuousHull7, SpecialContinuousHull8, SpecialContinuousHull9, " +
                "SpecialContinuousHull10, SpecialContinuousHull11, SpecialContinuousHull12, SpecialContinuousHull13, " +
                "SpecialContinuousHull14, SpecialContinuousHull15, SpecialHullNo) " +
                "VALUES (@ProjectId, @ImoNum, @ClassSurveyDate, @DryDockDate, " +
                "@SpecialContinuousMachineryDate, @BoilerDate, @TailshaftDate, @SpecialContinuousHull1, " +
                "@SpecialContinuousHull2, @SpecialContinuousHull3, @SpecialContinuousHull4, @SpecialContinuousHull5, " +
                "@SpecialContinuousHull6, @SpecialContinuousHull7, @SpecialContinuousHull8, @SpecialContinuousHull9, " +
                "@SpecialContinuousHull10, @SpecialContinuousHull11, @SpecialContinuousHull12, @SpecialContinuousHull13, " +
                "@SpecialContinuousHull14, @SpecialContinuousHull15, @SpecialHullNo)",
                list, tx);
        }

        private static void InsertReportStatutoryCertificates(SqliteConnection c, IDbTransaction tx, List<TransferReportStatutoryCertificates> list)
        {
            c.Execute(
                "INSERT INTO ReportStatutoryCertificates (ProjectId, LlliIssueDate, LlliExpiryDate, SafetyConIssueDate, " +
                "SafetyConExpiryDate, SafetyRadioIssueDate, SafetyRadioExpiryDate, SafetyEquipmentIssueDate, " +
                "SafetyEquipmentExpiryDate, IoppIssueDate, IoppExpiryDate, IapIssueDate, IapExpiryDate, IafsIsuueDate, " +
                "IafsExpiryDate, IsmIssueDate, IsmExpiryDate, IsExpiryDate, IspsIssueDate, IspsExpiryDate, " +
                "CargoGearReTestIssueDate, CargoGearReTestExpiryDate, CargoGearAnnualIssueCertificate, " +
                "CargoGearAnnualExpiryCertificate, IbwmIssueDate, IbwmExpiryDate, IsppSewageIssueDate, " +
                "IsppSewageExpiryDate, IeeIssueDate, IeeExpiryDate, MaritimeLabourIssueDate, MaritimeLabourExpiryDate,fitnessGasCertificateIssueDate,fitnessGasCertificateExpiryDate) " +
                "VALUES (@ProjectId, @LlliIssueDate, @LlliExpiryDate, @SafetyConIssueDate, @SafetyConExpiryDate, " +
                "@SafetyRadioIssueDate, @SafetyRadioExpiryDate, @SafetyEquipmentIssueDate, @SafetyEquipmentExpiryDate, " +
                "@IoppIssueDate, @IoppExpiryDate, @IapIssueDate, @IapExpiryDate, @IafsIsuueDate, @IafsExpiryDate, " +
                "@IsmIssueDate, @IsmExpiryDate, @IsExpiryDate, @IspsIssueDate, @IspsExpiryDate, @CargoGearReTestIssueDate, " +
                "@CargoGearReTestExpiryDate, @CargoGearAnnualIssueCertificate, @CargoGearAnnualExpiryCertificate, " +
                "@IbwmIssueDate, @IbwmExpiryDate, @IsppSewageIssueDate, @IsppSewageExpiryDate, @IeeIssueDate, @IeeExpiryDate, " +
                "@MaritimeLabourIssueDate, @MaritimeLabourExpiryDate,@fitnessGasCertificateIssueDate,@fitnessGasCertificateExpiryDate)",
                list, tx);
        }

        private static void InsertReportParts(SqliteConnection c, IDbTransaction tx, List<TransferReportPart> list)
        {
            c.Execute(@"
            INSERT INTO ReportPart (
                PartId, ProjectId, PartName, SequenceNo, IsActive,
                CreatedBy, CreatedOn, IsDeleted, ModifiedBy, ModifiedOn)
            VALUES (
                @PartId, @ProjectId, @PartName, @SequenceNo, @IsActive,
                @CreatedBy, @CreatedOn, @IsDeleted, @ModifiedBy, @ModifiedOn);
        ", list, tx);
        }

        private static void InsertProjectTankSectionMappings(SqliteConnection c, IDbTransaction tx, List<ProjectTankSectionMapping> list)
        {
            c.Execute(@"
            INSERT INTO ProjectTankSectionMapping (
                TankSectionMappingId, ProjectId, PartId, TankTypeId, TotalCards,
                PlaceholderCount, FileNameCount, IsActive, CreatedBy, CreatedOn,
                IsDeleted, ModifiedBy, ModifiedOn, IsSync
            )
            VALUES (
                @TankSectionMappingId, @ProjectId, @PartId, @TankTypeId, @TotalCards,
                @PlaceholderCount, @FileNameCount, @IsActive, @CreatedBy, @CreatedOn,
                @IsDeleted, @ModifiedBy, @ModifiedOn, @IsSync
            );
        ", list, tx);
        }

        private static void InsertProjectSectionMappings(SqliteConnection c, IDbTransaction tx, List<ProjectSectionMapping> list)
        {
            c.Execute(@"
            INSERT INTO ProjectSectionMapping (
                SectionMappingId, ProjectId, PartId, SectionName, SubHeader, TotalCards,
                PlaceholderCount, FileNameCount, IsActive, CreatedBy, CreatedOn, IsDeleted,
                HasSubSection, [Order], ModifiedBy, ModifiedOn, IsSync, RequiredInReport)
            VALUES (
                @SectionMappingId, @ProjectId, @PartId, @SectionName, @SubHeader, @TotalCards,
                @PlaceholderCount, @FileNameCount, @IsActive, @CreatedBy, @CreatedOn, @IsDeleted,
                @HasSubSection, @Order, @ModifiedBy, @ModifiedOn, @IsSync, @RequiredInReport);
        ", list, tx);
        }

        private static void InsertProjectSubSections(SqliteConnection c, IDbTransaction tx, List<DigitalCap.Core.Models.ProjectSubSection> list)
        {
            c.Execute(@"
            INSERT INTO ProjectSubSection (
                Id, ProjectSectionId, SectionName, Totalcards, FileNameCount,
                PlaceholderCount, IsActive, CreatedBy, CreatedOn, IsDeleted,
                ModifiedOn, ModifiedBy, IsSync,OrderNumber)
            VALUES (
                @Id, @ProjectSectionId, @SectionName, @Totalcards, @FileNameCount,
                @PlaceholderCount, @IsActive, @CreatedBy, @CreatedOn, @IsDeleted,
                @ModifiedOn, @ModifiedBy, @IsSync,@OrderNumber);
        ", list, tx);
        }

        private static void InsertProjectSectionGradingMappings(SqliteConnection c, IDbTransaction tx, List<ProjectSectionGradingMapping> list)
        {
            c.Execute(@"
            INSERT INTO ProjectSectionGradingMapping (
                SectionId, CategoryId, IsActive, CreatedBy, CreatedOn, IsDeleted,
                ModifiedOn, ModifiedBy, IsSync)
            VALUES (
                @SectionId, @CategoryId, @IsActive, @CreatedBy, @CreatedOn, @IsDeleted,
                @ModifiedOn, @ModifiedBy, @IsSync);
        ", list, tx);
        }

        private static void InsertProjectSectionDescriptionMappings(SqliteConnection c, IDbTransaction tx, List<ProjectSectionDescriptionMapping> list)
        {
            c.Execute(@"
            INSERT INTO ProjectSectionDescriptionMapping (
                SectionId, CategoryId, IsActive, CreatedBy, CreatedOn, IsDeleted,
                ModifiedOn, ModifiedBy, IsSync)
            VALUES (
                @SectionId, @CategoryId, @IsActive, @CreatedBy, @CreatedOn, @IsDeleted,
                @ModifiedOn, @ModifiedBy, @IsSync);
        ", list, tx);
        }

        private static void InsertProjectSubSectionDescriptionMappings(SqliteConnection c, IDbTransaction tx, List<ProjectSubSectionDescriptionMapping> list)
        {
            c.Execute(@"
            INSERT INTO ProjectSubSectionDescriptionMapping (
                SubSectionId, CategoryId, IsActive, CreatedBy, CreatedOn, IsDeleted,
                ModifiedOn, ModifiedBy, IsSync)
            VALUES (
                @SubSectionId, @CategoryId, @IsActive, @CreatedBy, @CreatedOn, @IsDeleted,
                @ModifiedOn, @ModifiedBy, @IsSync);
        ", list, tx);
        }

        private static void InsertHIGrading(SqliteConnection c, IDbTransaction tx, List<H_I_Grading> list)
        {
            c.Execute(@"
            INSERT INTO H_I_Grading (
                Id, LabelName, GradingConditionGroupId, SpecialGasGrading)
            VALUES (
                @Id, @LabelName, @GradingConditionGroupId, @SpecialGasGrading);
        ", list, tx);
        }

        private static void InsertProjectGrades(SqliteConnection c, IDbTransaction tx, List<CapProjectGrade> list)
        {
            c.Execute(@"
                INSERT INTO ProjectGrades (
                    Id, ProjectId, CAPCertificateNumber, CAPCertificateIssuanceDate,
                    ClassReportNumber, ClassReportDate, StructuralReportNumber,
                    StructuralReportDate, FinalGrade, StructuralGrade, FatigueGrade,
                    RenewalGrade, MaterialGrade, GaugingGrade, HullGirderStrength,
                    CAP1CertificateIssuanceDate, CAP1FinalGrade)
                VALUES (
                    @Id, @ProjectId, @CAPCertificateNumber, @CAPCertificateIssuanceDate,
                    @ClassReportNumber, @ClassReportDate, @StructuralReportNumber,
                    @StructuralReportDate, @FinalGrade, @StructuralGrade, @FatigueGrade,
                    @RenewalGrade, @MaterialGrade, @GaugingGrade, @HullGirderStrength,
                    @CAP1CertificateIssuanceDate, @CAP1FinalGrade);
            ", list, tx);
        }

        private static void InsertGradingData(SqliteConnection c, IDbTransaction tx, List<GradingData> list)
        {
            c.Execute(
                "INSERT INTO Grading (Id,LabelName,GradingGroupId,GradingConditionGroupId,IsActive,IsDeleted,VesselType,ProjectId,RequiredInReport) " +
                "VALUES (@Id,@LabelName,@GradingGroupId,@GradingConditionGroupId,@IsActive,@IsDeleted,@VesselType,@ProjectId,@RequiredInReport)",
                list.Select(g => new { g.Id, g.LabelName, g.GradingGroupId, g.GradingConditionGroupId, g.IsActive, g.IsDeleted, g.VesselType, g.ProjectId, g.RequiredInReport }), tx);
        }

        private static void InsertImageDescriptionsData(SqliteConnection c, IDbTransaction tx, List<ImageDescriptionsData> list)
        {
            c.Execute(
                "INSERT INTO ImageDescriptions (Id, Description, CategoryId, ProjectId, IsActive, CreatedDttm, UpdatedDttm, IsDeleted, VesselType) " +
                "VALUES (@Id, @Description, @CategoryId, @ProjectId, @IsActive, @CreatedDttm, @UpdatedDttm, @IsDeleted, @VesselType)",
                list.Select(x => new { x.Id, x.Description, x.CategoryId, x.ProjectId, x.IsActive, x.CreatedDttm, x.UpdatedDttm, x.IsDeleted, x.VesselType }), tx);
        }

        private static void InsertVesselTankMapping(SqliteConnection c, IDbTransaction tx, List<VesselTankMapping> list)
        {
            c.Execute(
                "INSERT INTO Vessel_TankMapping (Id, VesselType, ImoNumber, TankTypeId, TankName, IsActive, CreatedDttm, UpdateDttm, IsDeleted,RequiredInReport,ProjectId,Subheader) " +
                "VALUES (@Id, @VesselType, @ImoNumber, @TankTypeId, @TankName, @IsActive, @CreatedDttm, @UpdateDttm, @IsDeleted, @RequiredInReport,@ProjectId,@Subheader)",
                list.Select(m => new
                {
                    m.Id,
                    m.VesselType,
                    m.ImoNumber,
                    m.TankTypeId,
                    m.TankName,
                    m.IsActive,
                    CreatedDttm = m.CreatedDttm.ToString("yyyy-MM-dd HH:mm:ss"),
                    UpdateDttm = m.UpdateDttm.ToString("yyyy-MM-dd HH:mm:ss"),
                    m.IsDeleted,
                    m.RequiredInReport,
                    m.ProjectId,
                    m.Subheader
                }), tx);
        }

        private static void InsertVesselGradingMappings(SqliteConnection c, IDbTransaction tx, List<VesselGradingMapping> list)
        {
            c.Execute(
                @"INSERT INTO Vessel_GradingMapping (Id, VesselType, TankTypeId, GradingName, GradingGroupId, GradingConditionGroupId,
                IsActive, CreatedDttm, UpdateDttm, IsDeleted, ProjectId,RequiredInReport)
                VALUES (@Id, @VesselType, @TankTypeId, @GradingName, @GradingGroupId, @GradingConditionGroupId,
                @IsActive, @CreatedDttm, @UpdateDttm, @IsDeleted, @ProjectId,@RequiredInReport)",
                list.Select(m => new { m.Id, m.VesselType, m.TankTypeId, m.GradingName, m.GradingGroupId, m.GradingConditionGroupId, m.IsActive, m.CreatedDttm, m.UpdateDttm, m.IsDeleted, m.ProjectId, m.RequiredInReport }), tx);
        }

        private static void InsertVesselTypeTankMapping(SqliteConnection c, IDbTransaction tx, List<VesselTypeTankMapping> list)
        {
            // Vessel-type level (not per project): the reference replaces the vessel type's rows.
            c.Execute("DELETE FROM VesselTypeTankSectionMapping WHERE VesselTypeId = @VesselTypeId", new { VesselTypeId = list.First().VesselTypeId }, tx);
            c.Execute(
                @"INSERT INTO VesselTypeTankSectionMapping (VesselTypeTankSectionMappingId,VesselTypePartMappingId,VesselTypeId,TankTypeId,TotalCards,PlaceholderCount,FileNameCount,IsActive,CreatedBy,CreatedOn,IsDeleted,ModifiedBy,ModifiedOn)
                VALUES (@VesselTypeTankSectionMappingId,@VesselTypePartMappingId,@VesselTypeId,@TankTypeId,@TotalCards,@PlaceholderCount,@FileNameCount,@IsActive,@CreatedBy,@CreatedOn,@IsDeleted,@ModifiedBy,@ModifiedOn)",
                list, tx);
        }

        private static void InsertProjectGradingMapping(SqliteConnection c, IDbTransaction tx, List<ProjectGradingMapping> list)
        {
            c.Execute(
                "INSERT INTO ProjectGradingMapping (Id, ProjectId, TemplateId, SectionId, GradingId, Value, IsDeleted, CreatedDttm, UpdatedDttm) " +
                "VALUES (@Id, @ProjectId, @TemplateId, @SectionId, @GradingId, @Value, @IsDeleted, @CreatedDttm, @UpdatedDttm)",
                list.DistinctBy(m => m.Id).Select(m => new
                {
                    m.Id,
                    m.ProjectId,
                    m.TemplateId,
                    SectionId = m.SectionId.ToString().ToUpper(),
                    m.GradingId,
                    m.Value,
                    m.IsDeleted,
                    m.CreatedDttm,
                    m.UpdatedDttm
                }), tx);
        }

        private static void InsertProjectGradingConditionMapping(SqliteConnection c, IDbTransaction tx, List<ProjectGradingConditionMapping> list)
        {
            c.Execute(
                "INSERT INTO ProjectGradingConditionMapping (Id, ProjectId, TemplateId, SectionId, GradingId, GradingConditionLabel, GradingConditionValue, IsDeleted, CreatedDttm, UpdatedDttm) " +
                "VALUES (@Id, @ProjectId, @TemplateId, @SectionId, @GradingId, @GradingConditionLabel, @GradingConditionValue, @IsDeleted, @CreatedDttm, @UpdatedDttm)",
                list.DistinctBy(m => m.Id).Select(m => new
                {
                    m.Id,
                    m.ProjectId,
                    m.TemplateId,
                    SectionId = m.SectionId.ToString().ToUpper(),
                    m.GradingId,
                    m.GradingConditionLabel,
                    m.GradingConditionValue,
                    m.IsDeleted,
                    m.CreatedDttm,
                    m.UpdatedDttm
                }), tx);
        }

        private static void InsertProjectTemplateMapping(SqliteConnection c, IDbTransaction tx, List<ProjectReportMapping> list)
        {
            c.Execute(
                "INSERT INTO ProjectTemplateMapping (Id, ProjectId, TemplateId, SectionId, Notes, IsDeleted, CreatedDttm, UpdatedDttm) " +
                "VALUES (@Id, @ProjectId, @TemplateId, @SectionId, @Notes, @IsDeleted, @CreatedDttm, @UpdatedDttm)",
                list.DistinctBy(m => m.Id).Select(m => new
                {
                    m.Id,
                    m.ProjectId,
                    m.TemplateId,
                    SectionId = m.SectionId.ToString().ToUpper(),
                    m.Notes,
                    m.IsDeleted,
                    m.CreatedDttm,
                    m.UpdatedDttm
                }), tx);
        }

        private static void InsertProjectCardMapping(SqliteConnection c, IDbTransaction tx, List<ProjectCardMapping> list)
        {
            c.Execute(
                "INSERT INTO ProjectImageCards (Id, ProjectId, TemplateId, SectionId, CardId, DescriptionId, AdditionalDescription, CurrentCondition, ImageId, IsDeleted, CreatedDttm, UpdateDttm) " +
                "VALUES (@Id, @ProjectId, @TemplateId, @SectionId, @CardId, @DescriptionId, @AdditionalDescription, @CurrentCondition, @ImageId, @IsDeleted, @CreatedDttm, @UpdateDttm)",
                list.DistinctBy(m => m.Id).Select(m => new
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
                    m.CreatedDttm,
                    m.UpdateDttm
                }), tx);
        }

        private static void InsertGenericCardMapping(SqliteConnection c, IDbTransaction tx, List<GenericImageCard> list)
        {
            c.Execute(
                "INSERT INTO GenericImageCard (Id, ProjectId, TemplateId, SectionId, CardNumber, DescriptionId, AdditionalDescription, CurrentCondition, ImageId, IsDeleted, CreatedDttm, UpdatedDttm,CardName,IsActive) " +
                "VALUES (@Id, @ProjectId, @TemplateId, @SectionId, @CardNumber, @DescriptionId, @AdditionalDescription, @CurrentCondition, @ImageId, @IsDeleted, @CreatedDttm, @UpdatedDttm,@CardName,@IsActive)",
                list.DistinctBy(m => m.Id).Select(m => new
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
                    m.CreatedDttm,
                    m.UpdatedDttm,
                    m.CardName,
                    m.IsActive,
                }), tx);
        }

        private static void InsertTankImageCard(SqliteConnection c, IDbTransaction tx, List<TankImageCard> list)
        {
            c.Execute(
                "INSERT INTO TankImageCard (Id, ProjectId, TemplateId, SectionId, CardNumber, CardName, DescriptionId, AdditionalDescription, CurrentCondition, ImageId, CreatedDttm, UpdatedDttm, IsActive) " +
                "VALUES (@Id, @ProjectId, @TemplateId, @SectionId, @CardNumber, @CardName, @DescriptionId, @AdditionalDescription, @CurrentCondition, @ImageId, @CreatedDttm, @UpdatedDttm, @IsActive)",
                list.DistinctBy(m => m.Id).Select(m => new
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
                    m.CreatedDttm,
                    m.UpdatedDttm,
                    m.IsActive
                }), tx);
        }

        private static void InsertImages(SqliteConnection c, IDbTransaction tx, List<Images> list)
        {
            // Same image can be referenced by several sections; the reference skips existing ids.
            c.Execute(
                "INSERT INTO Images  (Id,AssignmentId,FileId,InsertDate,ProjectId) SELECT @Id,@AssignmentId,@FileId,@InsertDate,@ProjectId WHERE NOT EXISTS ( SELECT 1   FROM Images   WHERE Id = @Id);",
                list.Select(m => new { m.Id, m.AssignmentId, m.FileId, m.InsertDate, m.ProjectId }), tx);
        }

        private static void InsertImageData(SqliteConnection c, IDbTransaction tx, List<UpskillImageData> list)
        {
            c.Execute(
                "INSERT INTO ImageData  (Id, ImageId, Base64String, CreateDttm, UpdateDttm,ProjectId) SELECT @Id, @ImageId, @Base64String, @CreateDttm, @UpdateDttm,@ProjectId WHERE NOT EXISTS ( SELECT 1   FROM ImageData   WHERE Id = @Id);",
                list.Select(m => new { m.Id, m.ImageId, m.Base64String, m.CreateDttm, m.UpdateDttm, m.ProjectId }), tx);
        }

        // ================================================================ scoped delete / backup helpers

        private static async Task<string?> GetImo(SqliteConnection c, IDbTransaction? tx, int projectId, bool fromBackup)
        {
            var table = fromBackup ? "Projects" + BackupSuffix : "Projects";
            var imo = await c.ExecuteScalarAsync<object>($"SELECT IMO FROM {table} WHERE ID=@ProjectId LIMIT 1", new { ProjectId = projectId }, tx);
            return imo == null || imo is DBNull ? null : Convert.ToString(imo, CultureInfo.InvariantCulture);
        }

        private static void DeleteScoped(SqliteConnection c, IDbTransaction tx, string suffix, object scope)
        {
            foreach (var (table, predicate) in ProjectTables)
            {
                c.Execute($"DELETE FROM [{table}{suffix}] WHERE {string.Format(CultureInfo.InvariantCulture, predicate, suffix)}", scope, tx);
            }
        }

        /// <summary>
        /// Copies the project's rows between live and backup tables with explicit column lists
        /// (the reference used SELECT *, which breaks where column order differs, e.g. Vessel_TankMapping_backup).
        /// </summary>
        private static void CopyScoped(SqliteConnection c, IDbTransaction tx, string fromSuffix, string toSuffix, object scope)
        {
            foreach (var (table, predicate) in ProjectTables)
            {
                var source = table + fromSuffix;
                var target = table + toSuffix;
                var targetColumns = new HashSet<string>(GetColumns(c, tx, target), StringComparer.OrdinalIgnoreCase);
                var columns = GetColumns(c, tx, source).Where(targetColumns.Contains).Select(x => $"[{x}]").ToList();
                if (columns.Count == 0)
                    throw new InvalidOperationException($"No common columns between {source} and {target}.");
                var list = string.Join(", ", columns);
                c.Execute($"INSERT INTO [{target}] ({list}) SELECT {list} FROM [{source}] WHERE {string.Format(CultureInfo.InvariantCulture, predicate, fromSuffix)}", scope, tx);
            }
        }

        private static void EnsureBackupTablesExist(SqliteConnection c, IDbTransaction tx)
        {
            var missing = ProjectTables.Select(t => t.Table + BackupSuffix).Where(t => !GetColumns(c, tx, t).Any()).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException("Missing backup tables: " + string.Join(", ", missing));
        }

        private static List<string> GetColumns(SqliteConnection c, IDbTransaction? tx, string table)
            => c.Query<string>("SELECT name FROM pragma_table_info(@table)", new { table }, tx).AsList();

        // ================================================================ reading rows into models

        /// <summary>
        /// Dapper cannot convert SQLite TEXT to Guid, so rows are read as dictionaries and mapped here
        /// (the reference did the same through GenericMethods.ConvertDataTableToList). NULL leaves the default.
        /// </summary>
        private static async Task<List<T>> Read<T>(SqliteConnection c, string sql, object parameters, CancellationToken ct) where T : new()
        {
            var rows = await c.QueryAsync(new CommandDefinition(sql, parameters, cancellationToken: ct));
            var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite)
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var result = new List<T>();
            foreach (IDictionary<string, object> row in rows)
            {
                var item = new T();
                foreach (var (column, value) in row)
                {
                    if (value == null || value is DBNull || !props.TryGetValue(column, out var prop))
                        continue;
                    try
                    {
                        prop.SetValue(item, ConvertValue(value, prop.PropertyType));
                    }
                    catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
                    {
                        throw new FormatException($"Cannot convert column {column} value '{value}' to {prop.PropertyType.Name} for {typeof(T).Name}.", ex);
                    }
                }
                result.Add(item);
            }
            return result;
        }

        private static object? ConvertValue(object value, Type targetType)
        {
            var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (type.IsInstanceOfType(value))
                return value;

            if (type == typeof(string))
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            if (type == typeof(Guid))
                return value is byte[] bytes ? new Guid(bytes) : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!);
            if (type == typeof(bool))
            {
                if (value is string s)
                    return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
                return Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0;
            }
            if (type == typeof(DateTime))
                return value is string ds ? DateTime.Parse(ds, CultureInfo.InvariantCulture) : Convert.ToDateTime(value, CultureInfo.InvariantCulture);
            if (type == typeof(DateTimeOffset))
                return DateTimeOffset.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);
            if (type.IsEnum)
                return Enum.ToObject(type, Convert.ToInt64(value, CultureInfo.InvariantCulture));
            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
    }
}
