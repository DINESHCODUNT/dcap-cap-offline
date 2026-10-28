using Dapper;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Models.ExportConfig;
using DigitalCap.Core.Models.ReportConfig;
using DigitalCap.Core.Models.Survey;
using DigitalCap.Core.Models.Tank;
using DigitalCap.Persistence.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Transactions;

namespace DigitalCap.Persistence.Repositories
{
    public class ReportPartRepository : IReportPartRepository
    {
        protected readonly IUnitOfWork _unitOfWork;
        protected IDbConnection Connection => _unitOfWork?.Connection!;
        protected IDbTransaction Transaction => _unitOfWork?.Transaction!;
        public ReportPartRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        // ---- Local SQLite (desktop offline) queries. The SQL Server code below calls stored procedures that do not
        // exist locally; these are ported from digital-cap-offline ProjectReportOfflineService (SQLiteQueries.ProjectReport)
        // and corrected where the reference returned the wrong shape for the shared report services.

        // Reference GetProjectSubSection plus an explicit order (the report lists subsections in this order).
        private static readonly string SqliteProjectSubSection =
            DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetProjectSubSection + " ORDER BY P_SS.OrderNumber";

        // Generic image cards with the image (base64) as src, like config.GetGenericImageCard.
        private const string SqliteGenericImageCard = @"
SELECT GenericImageCard.Id, GenericImageCard.ProjectId, GenericImageCard.TemplateId, GenericImageCard.SectionId,
       CardName, CardNumber, DescriptionId, AdditionalDescription, CurrentCondition, GenericImageCard.ImageId,
       IsActive, ImageData.Base64String AS src
FROM GenericImageCard
LEFT OUTER JOIN ImageData ON GenericImageCard.ImageId = ImageData.ImageId
WHERE GenericImageCard.ProjectId = @ProjectId AND GenericImageCard.TemplateId = @TemplateId
  AND GenericImageCard.SectionId = @SectionId AND GenericImageCard.IsActive = 1
  AND (GenericImageCard.IsDeleted = 0 OR GenericImageCard.IsDeleted IS NULL)
ORDER BY CardNumber";

        // Tank image descriptions. The report service asks for every tank type (0) and filters by TankTypeId, so the
        // tank type is returned and 0 means all (the reference filtered on @TankTypeId only and returned no TankTypeId).
        private const string SqliteImageDescriptionsByTankTypeId = @"
SELECT DISTINCT descr.Id, descr.Description, tank.TankTypeId
FROM ImageDescriptions descr
INNER JOIN Tank_ImageDescriptionMapping tank ON descr.CategoryId = tank.CategoryId
INNER JOIN TankType tt ON tank.TankTypeId = tt.Id
WHERE descr.ProjectId = @ProjectId AND (@TankTypeId = 0 OR tank.TankTypeId = @TankTypeId)
  AND descr.IsActive = 1 AND (descr.IsDeleted = 0 OR descr.IsDeleted IS NULL)
ORDER BY descr.Id";

        // Count of sections the user may leave out of the export (RequiredInReport = 0) in one part, over the same
        // sections GetProjectExportSectionConfiguration lists. The reference query bound @ImoNumber against @Imonumber
        // (SQLite parameter names are case-sensitive, so it never matched) and joined tank mappings of every project.
        private const string SqliteCheckToShowSectionExportConfig = @"
SELECT
    (SELECT COUNT(*) FROM ProjectSectionMapping
      WHERE ProjectId = @ProjectId AND PartId = @PartId AND IsActive = 1 AND IsDeleted = 0 AND RequiredInReport = 0)
  + (SELECT COUNT(*) FROM Vessel_TankMapping vt
      WHERE vt.ProjectId = @ProjectId AND vt.ImoNumber = @ImoNumber AND vt.IsActive = 1 AND vt.IsDeleted = 0
        AND vt.RequiredInReport = 0
        AND EXISTS (SELECT 1 FROM ProjectTankSectionMapping ptsm
                     WHERE ptsm.ProjectId = @ProjectId AND ptsm.PartId = @PartId AND ptsm.TankTypeId = vt.TankTypeId
                       AND ptsm.IsActive = 1 AND ptsm.IsDeleted = 0))";

        // Export configuration of one part's sections: normal sections, their subsections (inheriting the parent's
        // RequiredInReport) and the project's tanks of the part's tank types. Reference GetProjectExportConfigForSections
        // with two fixes: "CAST(ParentId AS UNIQUEIDENTIFIER)" has INTEGER affinity in SQLite and turned the parent GUID
        // into a number, and the tank join duplicated tanks when a tank type had several mapping rows.
        private const string SqliteProjectExportSectionConfiguration = @"
SELECT SectionId, SectionName, RequiredInReport, TankType, TankTypeId, [Order], ParentId, IsSubSection
FROM (
    SELECT SectionMappingId AS SectionId, SectionName, RequiredInReport, NULL AS TankType, 0 AS TankTypeId,
           [Order], NULL AS ParentId, 0 AS IsSubSection, 0 AS SubOrder, NULL AS CreatedDttm
    FROM ProjectSectionMapping
    WHERE ProjectId = @ProjectId AND PartId = @PartId AND IsActive = 1 AND IsDeleted = 0

    UNION ALL

    SELECT sub.Id, sub.SectionName, section.RequiredInReport, NULL, 0,
           section.[Order], section.SectionMappingId, 1, sub.OrderNumber, NULL
    FROM ProjectSectionMapping section
    INNER JOIN ProjectSubSection sub ON sub.ProjectSectionId = section.SectionMappingId AND section.HasSubSection = 1
    WHERE section.ProjectId = @ProjectId AND section.PartId = @PartId AND section.IsActive = 1 AND section.IsDeleted = 0

    UNION ALL

    SELECT vt.Id, vt.TankName, vt.RequiredInReport, tt.TankType, tt.Id,
           999 + ROW_NUMBER() OVER (ORDER BY tt.Id, vt.TankName, vt.CreatedDttm), NULL, 0, 0, vt.CreatedDttm
    FROM Vessel_TankMapping vt
    INNER JOIN TankType tt ON tt.Id = vt.TankTypeId
    WHERE vt.ProjectId = @ProjectId AND vt.IsActive = 1 AND vt.IsDeleted = 0
      AND EXISTS (SELECT 1 FROM ProjectTankSectionMapping psm
                   WHERE psm.ProjectId = @ProjectId AND psm.PartId = @PartId AND psm.TankTypeId = vt.TankTypeId
                     AND psm.IsActive = 1 AND psm.IsDeleted = 0)
) AS result
ORDER BY [Order], IsSubSection, SubOrder, TankTypeId, CreatedDttm";
        public void Commit()
        {
            _unitOfWork?.Commit();
        }
        public async Task<bool> CreateProjectReportTemplate(int vesselTypeId, string userName, int projectId, bool imoExists = false, int copyprojectId = 0)
        {
            try
            {
                var result = await Connection.ExecuteAsync(
                      sql: "[Config].[CreateProject_Templates]",
                 commandType: CommandType.StoredProcedure,
                     param: new
                     {
                         VesselTypeId = vesselTypeId,
                         ProjectId = projectId,
                         UserName = userName,
                         IMOExists = imoExists,
                         CopyProjectId = copyprojectId
                     },
                 transaction: Transaction);
                this.Commit();

                return true;
            }
            catch (Exception ex)
            {

                return false;
            }
        }
        public async Task<ReportConfigDropDownDataList> GetVesselTypes()
        {
            if (Connection.IsSqlite())
                return new ReportConfigDropDownDataList
                {
                    vesselTypes = await Connection.QueryMappedAsync<VesselTypes>(
                        "SELECT Id AS VesselTypeId, VesselType FROM VesselType ORDER BY Id", transaction: Transaction)
                };

            try
            {
                var vesselTypes = await Connection.QueryAsync<VesselTypes>(
                    sql: "[Config].[Get_VesselTypes]",
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );

                return new ReportConfigDropDownDataList
                {
                    vesselTypes = vesselTypes.ToList()
                };
            }
            catch
            {
                return null;
            }
        }
        //reportpart
        public async Task<ReportConfigDropDownDataList> GetReportPartsByVesselTypeAsync(int vesselTypeId)
        {
            try
            {
                var reportTemplates = await Connection.QueryAsync<ReportTemplates>(
                    sql: "[Config].[Get_ReportPartsByVesselType]",
                    param: new { VesselTypeId = vesselTypeId },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );

                return new ReportConfigDropDownDataList
                {
                    reportTemplates = reportTemplates.ToList()
                };
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<ReportTemplates>> GetVesselTypePartMapping(int vesselTypeId)
        {
            try
            {
                var result = await Connection.QueryAsync<ReportTemplates>(
                    sql: "[Config].[Get_ReportPartsByVesselType]",
                    param: new { VesselTypeId = vesselTypeId },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );

                return result.ToList();
            }
            catch
            {
                return null;
            }
        }
        //reportpart
        public async Task<PartSectionNamesList> GetSectionNamesByPartNameAsync(int vesselTypeId, string partName)
        {
            try
            {
                using (var multi = await Connection.QueryMultipleAsync(
                    sql: "[Config].[Get_SectionNamesByPartName]",
                    param: new
                    {
                        VesselTypeId = vesselTypeId,
                        PartName = partName
                    },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                ))
                {
                    return new PartSectionNamesList
                    {
                        NormalSections = (await multi.ReadAsync<NormalSectionNames>()).ToList(),
                        TanksSections = (await multi.ReadAsync<TankSectionNames>()).ToList()
                    };
                }
            }
            catch
            {
                return null;
            }
        }
        //reportpart
        public async Task<PartSectionNamesList> GetSectionNamesByPartId(int vesselTypeId, int partId)
        {
            try
            {
                using (var multi = await Connection.QueryMultipleAsync(
                    sql: "[config].[GetVesselTypeSectionNormalMapping]",
                    param: new
                    {
                        PartId = partId,
                        VesselTypeId = vesselTypeId
                    },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                ))
                {
                    return new PartSectionNamesList
                    {
                        NormalSections = (await multi.ReadAsync<NormalSectionNames>()).ToList(),
                        SubSections = (await multi.ReadAsync<SubSectionNames>()).ToList(),
                        TanksSections = (await multi.ReadAsync<TankSectionNames>()).ToList()
                    };
                }
            }
            catch
            {
                return null;
            }
        }
        //reportpart
        public async Task<bool> CreateReportPart(VesselTypeReportConfigList reportConfigList, int vesselTypeId, string modifiedBy)
        {
            try
            {
                var dtParts = BuildReportParts(reportConfigList.reportParts, vesselTypeId, modifiedBy);
                var dtSections = BuildNormalSections(reportConfigList.normalSectionMappings, modifiedBy);
                var dtTankSections = BuildTankSections(reportConfigList.tankSectionMappings, modifiedBy);
                var dtSubSections = BuildSubSections(reportConfigList.normalSubSectionMappings, modifiedBy);

                await Connection.ExecuteAsync(
                    sql: "[Config].[Create_VesseltypeReportConfig]",
                    param: new
                    {
                        VesselTypeId = vesselTypeId,
                        ReportParts = dtParts.AsTableValuedParameter("[Config].[VesselTypePartMappingTbl]"),
                        NormalSectionMappings = dtSections.AsTableValuedParameter("[Config].[VesselTypeNormalSectionMappingTbl]"),
                        TankSectionMappings = dtTankSections.AsTableValuedParameter("[Config].[VesselTypeTankSectionMappingTbl]"),
                        SubSectionMappings = dtSubSections.AsTableValuedParameter("[Config].[VesselTypeSubSectionMappingTbl]")
                    },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );
                this.Commit();
                return true;
            }
            catch
            {
                return false;
            }
        }

        //  private DataTable BuildReportParts(
        //IEnumerable<VesselTypePartMapping>? parts,
        //int vesselTypeId,
        //string modifiedBy)
        //  {
        //      var dt = new DataTable();

        //      dt.Columns.Add("VesselTypePartMappingId", typeof(int));
        //      dt.Columns.Add("VesselTypeId", typeof(int));
        //      dt.Columns.Add("PartName", typeof(string));
        //      dt.Columns.Add("SequenceNo", typeof(int));
        //      dt.Columns.Add("IsActive", typeof(bool));
        //      dt.Columns.Add("CreatedBy", typeof(string));
        //      dt.Columns.Add("IsDeleted", typeof(bool));
        //      dt.Columns.Add("ModifiedBy", typeof(string));


        //      if (parts == null || !parts.Any())
        //          return dt;

        //      foreach (var p in parts)
        //      {
        //          dt.Rows.Add(
        //              p.vesselTypePartMappingId,
        //              vesselTypeId,
        //              p.PartName,
        //              p.SequenceNo,
        //              p.IsActive,
        //              string.IsNullOrEmpty(p.CreatedBy) ? modifiedBy : p.CreatedBy,
        //              p.IsDeleted,          
        //              modifiedBy
        //          );
        //      }

        //      return dt;
        //  }

        private DataTable BuildReportParts(
    IEnumerable<VesselTypePartMapping>? parts,
    int vesselTypeId,
    string modifiedBy)
        {
            var dt = new DataTable();

            dt.Columns.Add("VesselTypePartMappingId", typeof(int));
            dt.Columns.Add("VesselTypeId", typeof(int));
            dt.Columns.Add("PartName", typeof(string));
            dt.Columns.Add("SequenceNo", typeof(int));
            dt.Columns.Add("IsActive", typeof(bool));
            dt.Columns.Add("CreatedBy", typeof(string));
            dt.Columns.Add("IsDeleted", typeof(bool));
            dt.Columns.Add("ModifiedBy", typeof(string));

            if (parts == null || !parts.Any())
                return dt;  

            foreach (var p in parts)
            {
                dt.Rows.Add(
      p.vesselTypePartMappingId == 0 ? DBNull.Value : p.vesselTypePartMappingId,
      vesselTypeId,
                    p.PartName,
                    p.SequenceNo,
                    p.IsActive,
      string.IsNullOrEmpty(p.CreatedBy) ? modifiedBy : p.CreatedBy,
      p.IsDeleted ?? false,
                    modifiedBy
                );

            }

            return dt;
        }

        private DataTable BuildNormalSections(IEnumerable<VesselTypeSectionMapping>? sections, string modifiedBy)
        {
            var dt = new DataTable();
            dt.Columns.Add("VesselTypeNormalSectionMappingId", typeof(Guid));
            dt.Columns.Add("VesselTypePartMappingId", typeof(int));
            dt.Columns.Add("VesselTypeId", typeof(int));
            dt.Columns.Add("PartName", typeof(string));
            dt.Columns.Add("SectionName", typeof(string));
            dt.Columns.Add("SubHeader", typeof(string));
            dt.Columns.Add("TotalCards", typeof(int));
            dt.Columns.Add("PlaceholderCount", typeof(int));
            dt.Columns.Add("FileNameCount", typeof(int));
            dt.Columns.Add("IsActive", typeof(bool));
            dt.Columns.Add("CreatedBy", typeof(string));
            dt.Columns.Add("IsDeleted", typeof(bool));
            dt.Columns.Add("ModifiedBy", typeof(string));

            if (sections == null || !sections.Any())
            {
                dt.Rows.Add(dt.NewRow());
                return dt;
            }

            foreach (var s in sections)
            {
                dt.Rows.Add(
                    s.VesselTypeNormalSectionMappingId ?? Guid.Empty,
                    s.VesselTypePartMappingId,
                    s.VesselTypeId,
                    s.PartName,
                    s.SectionName,
                    s.SubHeader,
                    s.TotalCards,
                    s.PlaceholderCount,
                    s.FileNameCount,
                    s.IsActive,
                    s.CreatedBy,
                    s.IsDeleted,
                    modifiedBy
                );
            }

            return dt;
        }

        private DataTable BuildTankSections(
          IEnumerable<VesselTypeTankSectionMapping>? tanks,
          string modifiedBy)
        {
            var dt = new DataTable();

            dt.Columns.Add("VesselTypePartMappingId", typeof(int));
            dt.Columns.Add("VesselTypeId", typeof(int));
            dt.Columns.Add("PartName", typeof(string));
            dt.Columns.Add("TankTypeId", typeof(int));
            dt.Columns.Add("TotalCards", typeof(int));
            dt.Columns.Add("PlaceholderCount", typeof(int));
            dt.Columns.Add("FileNameCount", typeof(int));
            dt.Columns.Add("IsActive", typeof(bool));
            dt.Columns.Add("CreatedBy", typeof(string));
            dt.Columns.Add("IsDeleted", typeof(bool));
            dt.Columns.Add("ModifiedBy", typeof(string));

            if (tanks == null || !tanks.Any())
            {
                dt.Rows.Add(dt.NewRow());
                return dt;
            }

            foreach (var t in tanks)
            {
                dt.Rows.Add(
                    t.VesselTypePartMappingId == 0 ? DBNull.Value : t.VesselTypePartMappingId,
                    t.VesselTypeId == 0 ? DBNull.Value : t.VesselTypeId,
                    string.IsNullOrWhiteSpace(t.PartName) ? DBNull.Value : t.PartName,
                    t.TankTypeId,
                    t.TotalCards,
                    t.PlaceholderCount,
                    t.FileNameCount,
                    t.IsActive,
                    t.CreatedBy ?? modifiedBy,
                    t.IsDeleted,
                    modifiedBy
                );
            }

            return dt;
        }

        private DataTable BuildSubSections(IEnumerable<VesselTypeSubSectionMapping>? subSections, string modifiedBy)
        {
            var dt = new DataTable();
            dt.Columns.Add("SubSectionId", typeof(Guid));
            dt.Columns.Add("SectionId", typeof(Guid));
            dt.Columns.Add("SectionName", typeof(string));
            dt.Columns.Add("TotalCards", typeof(int));
            dt.Columns.Add("PlaceholderCount", typeof(int));
            dt.Columns.Add("FileNameCount", typeof(int));
            dt.Columns.Add("IsActive", typeof(bool));
            dt.Columns.Add("IsDeleted", typeof(bool));
            dt.Columns.Add("CreatedBy", typeof(string));
            dt.Columns.Add("ModifiedBy", typeof(string));

            if (subSections == null || !subSections.Any())
            {
                dt.Rows.Add(dt.NewRow());
                return dt;
            }

            foreach (var s in subSections)
            {
                dt.Rows.Add(
                    s.SubSectionId,
                    s.SectionId,
                    s.SectionName,
                    s.TotalCards,
                    s.PlaceholderCount,
                    s.FileNameCount,
                    s.IsActive,
                    s.IsDeleted,
                    s.CreatedBy,
                    modifiedBy
                );
            }

            return dt;
        }

        public async Task<bool> CheckToShowSectionExportConfig(int projectId, int imoNumber, int partId)
        {
            if (Connection.IsSqlite())
                return await Connection.ExecuteScalarAsync<long>(SqliteCheckToShowSectionExportConfig,
                    new { ProjectId = projectId, ImoNumber = imoNumber, PartId = partId }, Transaction) > 0;

            try
            {
                var result = await Connection.ExecuteScalarAsync<int>(
                    sql: "Config.CheckToShowSectionExportConfig",
                    param: new
                    {
                        ProjectId = projectId,
                        ImoNumber = imoNumber,
                        PartId = partId
                    },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );

                return result == 1;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> UpdateGenericImageCard(GenericImageCard card)
        {
            try
            {
                if (Connection.IsSqlite())
                {
                    var p = new DynamicParameters();
                    p.Add("@Id", card.Id);
                    p.Add("@ProjectId", card.ProjectId);
                    p.Add("@TemplateId", card.TemplateId);
                    p.Add("@SectionId", card.SectionId);
                    p.Add("@CardNumber", card.CardNumber);
                    p.Add("@DescriptionId", card.DescriptionId);
                    p.Add("@AdditionalDescription", card.AdditionalDescription ?? "");
                    p.Add("@CurrentCondition", card.CurrentCondition);
                    p.Add("@ImageId", card.ImageId);
                    p.Add("@CreatedDttm", card.CreatedDttm == DateTime.MinValue ? DateTime.Now : card.CreatedDttm);
                    p.Add("@UpdatedDttm", DateTime.Now);
                    p.Add("@IsDeleted", card.IsDeleted ? 1 : 0);
                    p.Add("@IsActive", card.IsActive ? 1 : 0);
                    p.Add("@CardName", card.CardName);

                    var rows = await Connection.ExecuteAsync(
                        DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.UpdateGenericImageCard,
                        p,
                        Transaction);
                    Commit();
                    return rows > 0;
                }

                await Connection.ExecuteAsync(
                    sql: "[config].[UpdateGenericImageCard]",
                    param: new
                    {
                        card.Id,
                        card.ProjectId,
                        card.TemplateId,
                        card.SectionId,
                        card.CardNumber,
                        card.DescriptionId,
                        card.AdditionalDescription,
                        card.CurrentCondition,
                        card.ImageId,
                        card.IsActive,
                        card.src
                    },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );
                this.Commit();
                return true;
            }
            catch
            {
                return false;
            }
        }
        public async Task<GenericImageCard?> GetGenericImageCardByName(int projectId, int templateId, Guid sectionId, int cardNumber)
        {
            try
            {
                if (Connection.IsSqlite())
                {
                    var list = await Connection.QueryMappedAsync<GenericImageCard>(
                        DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetGenericImageCardById,
                        new { ProjectId = projectId, TemplateId = templateId, SectionId = sectionId, CardNumber = cardNumber },
                        Transaction);
                    return list.FirstOrDefault();
                }

                var result = await Connection.QueryAsync<GenericImageCard>(
                    sql: "[config].[GetGenericImagecardByName]",
                    param: new
                    {
                        ProjectId = projectId,
                        TemplateId = templateId,
                        SectionId = sectionId,
                        CardNumber = cardNumber
                    },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );

                return result.FirstOrDefault();
            }
            catch
            {
                return null;
            }
        }
        public async Task<List<ProjectCheckBox>> GetGradingCondition()
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ProjectCheckBox>(
                    DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetGradigCondition + " ORDER BY GradingCondition.Id", transaction: Transaction);

            await using var connection = new SqlConnection(_unitOfWork.ConnectionString);

            await connection.OpenAsync();
            var result = await connection.QueryAsync<ProjectCheckBox>(
                sql: "[Config].[GetGradingCondition]",
                commandType: CommandType.StoredProcedure
            );

            return result.ToList();
        }
        public async Task<List<ProjectSubSection>> GetNormalSectionByProjectId(int projectId, int partId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ProjectSubSection>(
                    DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetNormalsectionsByProjectId,
                    new { ProjectId = projectId, PartId = partId }, Transaction);

            await using var connection = new SqlConnection(_unitOfWork.ConnectionString);

            await connection.OpenAsync();
            var result = await connection.QueryAsync<ProjectSubSection>(
                sql: "[config].[GetNormalsectionsByProjectId]",
                param: new
                {
                    ProjectId = projectId,
                    PartId = partId
                },
                commandType: CommandType.StoredProcedure
            );

            return result.ToList();
        }

        public async Task<List<ProjectSubSection>> GetProjectSubsectionBySectionId(Guid sectionId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ProjectSubSection>(
                    SqliteProjectSubSection, new { SectionId = sectionId }, Transaction);

            var result = await Connection.QueryAsync<ProjectSubSection>(
                sql: "[Config].[GetProjectSubsectionBySectionId]",
                param: new
                {
                    SectionId = sectionId
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result.ToList();
        }

        public async Task<List<ProjectCheckBox>> GetGradingConditionByProjectId(int projectId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ProjectCheckBox>(
                    DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetGradingConditionByProjectId,
                    new { ProjectId = projectId }, Transaction);

            var result = await Connection.QueryAsync<ProjectCheckBox>(
                sql: "[config].[GetGradigConditionByProjectId]",
                param: new
                {
                    ProjectId = projectId
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result.ToList();
        }
        public async Task<List<ProjectGradingUI>> GetGradingBySectionId(int projectId, Guid sectionId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ProjectGradingUI>(
                    DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetProjectSectionGrading,
                    new { ProjectId = projectId, SectionId = sectionId }, Transaction);

            var result = await Connection.QueryAsync<ProjectGradingUI>(
                sql: "[config].[GetProjectSectionGrading]",
                param: new
                {
                    ProjectId = projectId,
                    SectionId = sectionId
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result.ToList();
        }

        public async Task<List<ImageDescriptionUI>> GetImageDescriptionsBySectionId(int projectId, Guid sectionId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ImageDescriptionUI>(
                    DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetProjectSectionDescription,
                    new { ProjectId = projectId, SectionId = sectionId }, Transaction);

            var result = await Connection.QueryAsync<ImageDescriptionUI>(
                sql: "[config].[GetProjectSectionDescription]",
                param: new
                {
                    ProjectId = projectId,
                    SectionId = sectionId
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result.ToList();
        }
        public async Task<List<ImageDescriptionUI>> GetImageDescriptionsBySubSectionId(int projectId, Guid sectionId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ImageDescriptionUI>(
                    DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetProjectSubSectionDescription,
                    new { ProjectId = projectId, SectionId = sectionId }, Transaction);

            var result = await Connection.QueryAsync<ImageDescriptionUI>(
                sql: "[config].[GetProjectSubSectionDescription]",
                param: new
                {
                    ProjectId = projectId,
                    SectionId = sectionId
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result.ToList();
        }

        public async Task<List<GenericImageCard>> GetGenericImageCard(int projectId, int templateId, Guid sectionId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<GenericImageCard>(SqliteGenericImageCard,
                    new { ProjectId = projectId, TemplateId = templateId, SectionId = sectionId }, Transaction);

            var result = await Connection.QueryAsync<GenericImageCard>(
                sql: "[config].[GetGenericImageCard]",
                param: new
                {
                    ProjectId = projectId,
                    TemplateId = templateId,
                    SectionId = sectionId
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result.ToList();
        }

        public async Task<List<ProjectTankUI>> GetTankSectionByProjectId(int projectId, int partId, int imoNumber)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ProjectTankUI>(
                    DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetTankSectionsByProjectid,
                    new { ProjectId = projectId, PartId = partId, ImoNumber = imoNumber }, Transaction);

            var result = await Connection.QueryAsync<ProjectTankUI>(
                sql: "[config].[GetTankSectionsByProjectid]",
                param: new
                {
                    ProjectId = projectId,
                    PartId = partId,
                    ImoNumber = imoNumber
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result.ToList();
        }

        public async Task<List<Core.Models.Survey.Grading>> GetH_IGrading(bool isGasCarrier)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<Core.Models.Survey.Grading>(
                    DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetH_IGrading + " ORDER BY Id",
                    new { GasGrading = isGasCarrier }, Transaction);

            var result = await Connection.QueryAsync<Core.Models.Survey.Grading>(
                sql: "[Config].[GetH_IGrading]",
                param: new
                {
                    GasGrading = isGasCarrier
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result.ToList();
        }

        public async Task<List<ImageDescriptionUI>> GetImageDescriptionsByTankTypeId(int projectId, int tankTypeId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ImageDescriptionUI>(SqliteImageDescriptionsByTankTypeId,
                    new { ProjectId = projectId, TankTypeId = tankTypeId }, Transaction);

            await using var connection = new SqlConnection(_unitOfWork.ConnectionString);

            await connection.OpenAsync();
            var result = await connection.QueryAsync<ImageDescriptionUI>(
                sql: "[config].[GetImageDescriptionsByTankTypeId]",
                param: new
                {
                    ProjectId = projectId,
                    TankTypeId = tankTypeId
                },
                commandType: CommandType.StoredProcedure
            );

            return result.ToList();
        }

        public async Task<List<ProjectGradingConditionMapping>> GetHandIGradingConditionByProjectId(int projectId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ProjectGradingConditionMapping>(
                    DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetHandIProjectGradingConditionByProjectId,
                    new { ProjectId = projectId }, Transaction);

            try
            {
                var result = await Connection.QueryAsync<ProjectGradingConditionMapping>(
                   sql: "[config].[GetHandIProjectGradingConditionByProjectId]",
                    new { ProjectId = projectId },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );

                return result.ToList();
            }
            catch (SqlException)
            {
                throw; // or log
            }
        }


        public async Task<List<ProjectGradingMapping>> GetH_IProjectGradingByProjectId(int projectId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ProjectGradingMapping>(
                    DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetHandIProjectGradingByProjectId,
                    new { ProjectId = projectId }, Transaction);

            var result = await Connection.QueryAsync<ProjectGradingMapping>(
                sql: "[config].[GetHandIProjectGradingByProjectId]",
                param: new
                {
                    ProjectId = projectId
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result.ToList();
        }

        public async Task<ProjectPart> GetProjectTemplateTitle(int partId, int projectId)
        {
            if (Connection.IsSqlite())
                return (await Connection.QueryFirstMappedAsync<ProjectPart>(
                    DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetTemplateTitle,
                    new { PartId = partId, ProjectId = projectId }, Transaction))!;

            try
            {
                await using var connection = new SqlConnection(_unitOfWork.ConnectionString);

                await connection.OpenAsync();
                using (var multi = await connection.QueryMultipleAsync(
                    sql: "[config].[GetTemplateTitle]",
                    param: new
                    {
                        partId = partId,
                        projectId = projectId
                    },
                    commandType: CommandType.StoredProcedure
                ))
                {
                    return await multi.ReadFirstOrDefaultAsync<ProjectPart>();
                }
            }
            catch (Exception)
            {
                return null; // or throw / log
            }
        }
        public async Task<List<ExportPart>> GetVesselTypeExportPartConfiguration(int vesseltypeId)
        {
            // The vessel-type part configuration (config.VesselTypePartMapping) is not part of the local database;
            // offline a project's export configuration comes from its downloaded ReportPart rows only.
            if (Connection.IsSqlite())
                return new List<ExportPart>();

            DataTable dt = new DataTable();
            try
            {
                var result = await Connection.QueryAsync<ExportPart>(
                   sql: "[config].[GetVesselTypeExportConfigForParts]",
                   new
                   {
                       VesselTypeId = vesseltypeId,
                   },
                   commandType: CommandType.StoredProcedure,
                   transaction: Transaction);

                return result?.ToList();
            }

            catch (SqlException sqlEx)
            {
                //throw sqlEx;
            }
            return null;
        }

        public async Task<List<ExportSections>> GetVesselTypeExportSectionConfiguration(int partId, int vesselTypeId)
        {
            // See GetVesselTypeExportPartConfiguration: no vessel-type section configuration locally.
            if (Connection.IsSqlite())
                return new List<ExportSections>();

            DataTable dt = new DataTable();
            try
            {

                var result = await Connection.QueryAsync<ExportSections>(
                   sql: "[config].[GetVesselTypeExportConfigForSections]",//[config].[GetVesselTypeExportConfigForSections]
                   new
                   {
                       PartId = partId,
                       VesselTypeId = vesselTypeId

                   },
                   commandType: CommandType.StoredProcedure,
                   transaction: Transaction);

                return result?.ToList();
            }

            catch (SqlException sqlEx)
            {
                //throw sqlEx;
            }
            return null;
        }
        public async Task<List<ExportPart>> GetProjectExportPartConfiguration(int projectId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ExportPart>(
                    DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetProjectExportConfigForParts + " ORDER BY SequenceNo, PartId",
                    new { ProjectId = projectId }, Transaction);

            DataTable dt = new DataTable();
            try
            {
                var result = await Connection.QueryAsync<ExportPart>(
                   sql: "config.GetProjectExportConfigForParts",// 
                   new
                   {
                       Projectid = projectId,
                   },
                   commandType: CommandType.StoredProcedure,
                   transaction: Transaction);

                return result?.ToList();

            }

            catch (SqlException sqlEx)
            {
                //throw sqlEx;
            }
            return null;
        }

        public async Task<List<ExportSections>> GetProjectExportSectionConfiguration(int partId, int projectId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ExportSections>(SqliteProjectExportSectionConfiguration,
                    new { PartId = partId, ProjectId = projectId }, Transaction);

            DataTable dt = new DataTable();
            try
            {
                var result = await Connection.QueryAsync<ExportSections>(
                   sql: "config.GetProjectExportConfigForSections",
                   new
                   {
                       PartId = partId,
                       ProjectId = projectId
                   },
                   commandType: CommandType.StoredProcedure,
                   transaction: Transaction);

                return result?.ToList();
            }
            catch (SqlException sqlEx)
            {
                //throw sqlEx;
            }
            return null;
        }

    }

}

