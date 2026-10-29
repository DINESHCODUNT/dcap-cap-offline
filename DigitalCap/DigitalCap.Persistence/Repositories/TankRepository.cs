using Dapper;
using DigitalCap.Core.Enumerations;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.ReportConfig;
using DigitalCap.Core.Models.Tank;
using DigitalCap.Core.Models.VesselModel;
using DigitalCap.Core.Models.View.Admin;
using DigitalCap.Core.ViewModels;
using DigitalCap.Persistence.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Numerics;
using System.Text;

namespace DigitalCap.Persistence.Repositories
{
    public class TankRepository : RepositoryBase<VesselTank, Guid>, ITankRepository
    {
        private readonly ILogger<TankRepository> _logger;
        public TankRepository(IUnitOfWork unitOfWork, ILogger<TankRepository> logger) : base(unitOfWork, logger)
        {
            _logger = logger;
        }

        // Tank image cards with the image (base64) as src, like dbo.GetTankImageCard. The report services ask for every
        // section of a part with an empty section id and filter on SectionId, so the section id is returned and the
        // empty id means all (the reference query SQLiteQueries.Tank.GetProjectTankImageCard did neither).
        private const string SqliteProjectTankImageCard = @"
SELECT TankImageCard.Id, TankImageCard.ProjectId, TankImageCard.TemplateId, TankImageCard.SectionId, CardName, CardNumber,
       DescriptionId, AdditionalDescription, CurrentCondition, TankImageCard.ImageId, IsActive, ImageData.Base64String AS src
FROM TankImageCard
LEFT OUTER JOIN ImageData ON TankImageCard.ImageId = ImageData.ImageId
WHERE TankImageCard.ProjectId = @ProjectId AND TankImageCard.TemplateId = @TemplateId
  AND (@SectionId = '00000000-0000-0000-0000-000000000000' OR TankImageCard.SectionId = @SectionId)
  AND TankImageCard.IsActive = 1
ORDER BY TankImageCard.SectionId, CardNumber";

        public async Task<List<VesselTank>> GetTanks_VesselByIMO(string imonumber)
        {
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@ImoNumber", imonumber);

                return (await Connection.QueryAsync<VesselTank>(
                    sql: "dbo.Get_Vessel_Tank_ByIMONumber",
                    param: parameters,
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction)).ToList();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<List<VesselTank>> CreateTanks_VesselByProject(string imonumber, int projectId, int newprojectId, string newimo)
        {
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@ImoNumber", imonumber);

                return (await Connection.QueryAsync<VesselTank>(
                    sql: "dbo.CreateTanksofVesselByProject",
                    param: parameters,
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction)).ToList();
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<bool> PopulateTemplate(string imonumber = null, string vesseltype = null, string copyingimonumber = null)
        {
            try
            {
                var tanks = new List<VesselTank>();
                var gradings = new List<VesselTankGrading>();


                if (!string.IsNullOrEmpty(copyingimonumber))
                {
                    tanks = this.GetTanks_VesselByIMO(copyingimonumber).Result;


                    foreach (var tank in tanks)
                    {
                        tank.Id = Guid.NewGuid();
                        tank.ImoNumber = imonumber;
                        tank.CreatedDttm = DateTime.Now;
                        tank.UpdateDttm = DateTime.Now;
                        var result = this.CreateTank(tank).Result;
                    }

                }

                if (!string.IsNullOrEmpty(vesseltype))
                {
                    tanks = this.GetTanks_VesselByVesselType(vesseltype).Result;


                    foreach (var tank in tanks)
                    {
                        tank.Id = Guid.NewGuid();
                        tank.ImoNumber = imonumber;
                        tank.CreatedDttm = DateTime.Now;
                        tank.VesselType = null;
                        tank.UpdateDttm = DateTime.Now;
                        var result = this.CreateTank(tank).Result;
                    }

                }


                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        public async Task<bool> PopulateTemplate(string imonumber = null, string vesseltype = null, string copyingimonumber = null, int projectid = 0, int copyingProjectId = 0)
        {
            try
            {
                var tanks = new List<VesselTank>();
                if (!string.IsNullOrEmpty(copyingimonumber))
                {
                    tanks = copyingProjectId == 0 ? GetTanks_VesselByIMO(copyingimonumber).Result : CreateTanks_VesselByProject(copyingimonumber, copyingProjectId, projectid, imonumber).Result;

                }
                if (!string.IsNullOrEmpty(vesseltype))
                {
                    tanks = this.GetTanks_VesselByVesselType(vesseltype).Result;
                    foreach (var tank in tanks)
                    {
                        tank.Id = Guid.NewGuid();
                        tank.ImoNumber = imonumber;
                        tank.CreatedDttm = DateTime.Now;
                        tank.VesselType = null;
                        tank.UpdateDttm = DateTime.Now;
                        tank.ProjectId = projectid;
                        var result = this.CreateTank(tank).Result;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public async Task<List<VesselTank>> GetTanks_VesselByVesselType(string vesseltype)
        {

            try
            {
                //using (var connection = new SqlConnection(azureDbConnString))
                //{

                return (await Connection.QueryAsync<VesselTank>(
                    sql: "dbo.Get_Vessel_Tank_ByVesselType",
                    param: new
                    {
                        VesselType = vesseltype
                    },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction)).ToList();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<List<VesselTank>> GetTanks_VesselByDetails(string vesseltype, string imonumber)
        {
            try
            {
                return (await Connection.QueryAsync<VesselTank>(
                   sql: "dbo.Get_Vessel_Tank_ByDetails",
                   param: new
                   {
                       VesselType = vesseltype,
                       Imonumber = imonumber
                   },
                   commandType: CommandType.StoredProcedure,
                   transaction: Transaction)).ToList();
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<bool> CreateTank(VesselTank vesselTank)
        {
            try
            {
                var parameters = new DynamicParameters();

                parameters.Add("@Id", vesselTank.Id);
                parameters.Add("@VesselType", vesselTank.VesselType);
                parameters.Add("@TankName", vesselTank.TankName);
                parameters.Add("@Subheader", vesselTank.Subheader);
                parameters.Add("@TankTypeId", vesselTank.TankTypeId);
                parameters.Add("@ImoNumber", vesselTank.ImoNumber);
                parameters.Add("@TankTypeId", vesselTank.TankTypeId);
                parameters.Add("@TankName", vesselTank.TankName);
                parameters.Add("@IsActive", vesselTank.IsActive);
                parameters.Add("@IsDeleted", vesselTank.IsDeleted);
                parameters.Add("@CreatedDttm", vesselTank.CreatedDttm);
                parameters.Add("@UpdateDttm", vesselTank.UpdateDttm);
                parameters.Add("@IsDeleted", vesselTank.IsDeleted);
                parameters.Add("@Subheader", vesselTank.Subheader);
                parameters.Add("@RequiredInReport", vesselTank.RequiredInReport);
                parameters.Add("@ProjectId", vesselTank.ProjectId);

                await Connection.ExecuteAsync(
                            sql: "dbo.Create_Vessel_TankMapping",
                            param: parameters,
                            transaction: Transaction,
                            commandType: CommandType.StoredProcedure
                        );

                this.Commit();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating vessel tank");
                return false;
            }
        }

        public async Task<List<Core.Models.Tank.ShipType>> GetShipType()
        {
            try
            {
                if (Connection is Microsoft.Data.Sqlite.SqliteConnection || Connection.GetType().Name.Contains("Sqlite"))
                {
                    var sqliteResult = await Connection.QueryAsync<Core.Models.Tank.ShipType>(
                        sql: DigitalCap.Core.Constants.SQLiteQueries.Tank.GetShipType,
                        commandType: CommandType.Text,
                        transaction: Transaction
                    );
                    return sqliteResult.ToList();
                }

                var result = await Connection.QueryAsync<Core.Models.Tank.ShipType>(
                    sql: "dbo.GetShipType",
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );

                return result.ToList();

            }
            catch (Exception)
            {
                throw; // API layer can handle error
            }
        }

        public async Task<List<VesselTank>> GetTanks_VesselByProject(string imoNumber, int projectId)
        {
            try
            {
                var result = await Connection.QueryAsync<VesselTank>(
                      sql: "[dbo].[Get_Vessel_Tank_ByProject]",
                      param: new
                      {
                          ImoNumber = imoNumber,
                          ProjectId = projectId
                      },
                       transaction: Transaction,
                       commandType: CommandType.StoredProcedure
                  );
                return result.ToList();

            }
            catch
            {
                throw;
            }
        }
        public async Task<List<VesselTankGrading>> GetVessel_GradingByVesselType(string vesseltype)
        {
            try
            {
                var result = await Connection.QueryAsync<VesselTankGrading>(
                       sql: "dbo.Get_Vessel_Tank_Grading_ByVesselType",
                      param: new
                      {

                          VesselType = vesseltype
                      },
                       commandType: CommandType.StoredProcedure, transaction: Transaction);

                return result.ToList();

            }
            catch (Exception ex)
            {
                return null;
            }

        }

        public async Task<bool> CreateVessel_Grading(VesselTankGrading vesselTankGrading)
        {
            try
            {
                var parameters = new DynamicParameters();

                foreach (var prop in typeof(VesselTankGrading).GetProperties())
                {
                    if (prop.Name != "Id" &&
                        prop.Name != "TemplateId" &&
                        prop.Name != "VesselName")
                    {
                        parameters.Add("@" + prop.Name, prop.GetValue(vesselTankGrading));
                    }
                }

                await Connection.ExecuteAsync(
                    sql: "dbo.Create_Vessel_Grading",
                    param: parameters,
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );

                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        public async Task<List<TankUI>> GetTemplateTanks(int templateId, string imonumber, string vesseltype, int projectId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<TankUI>(DigitalCap.Core.Constants.SQLiteQueries.Tank.GetTempaleTanks,
                    new { TemplateId = templateId, ImoNumber = imonumber, VesselType = vesseltype, ProjectId = projectId }, Transaction);

            try
            {

                var result = await Connection.QueryAsync<TankUI>(
                    sql: "dbo.GetTempaleTanks",
                    param: new
                    {
                        TemplateId = templateId,
                        ImoNumber = imonumber,
                        VesselType = vesseltype,
                        ProjectId = projectId
                    },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction);

                return result.ToList();

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<List<TankCheckBox>> GetTemplateTankGradingCondition(int Id)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<TankCheckBox>(
                    DigitalCap.Core.Constants.SQLiteQueries.Tank.GetTankGradingConditionById + " ORDER BY GradingCondition.Id",
                    new { Id }, Transaction);

            try
            {
                await using var connection = new SqlConnection(_unitOfWork.ConnectionString);

                await connection.OpenAsync();
                var result = await connection.QueryAsync<TankCheckBox>(
                    sql: "dbo.GetTankGradingConditionById",
                      new
                      {
                          Id = Id
                      },
                    commandType: CommandType.StoredProcedure);

                return result.ToList();

            }
            catch (Exception ex)
            {
                return new List<TankCheckBox>();
            }
        }

        public async Task<List<TankGradingUI>> GetTemplateTankGrading(int tanktypeId, int projectId, string vesseltype)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<TankGradingUI>(DigitalCap.Core.Constants.SQLiteQueries.Tank.GetTempaleTankGrading,
                    new { TankTypeId = tanktypeId, ProjectId = projectId, VesselType = vesseltype }, Transaction);

            try
            {
                var result = await Connection.QueryAsync<TankGradingUI>(
                        sql: "dbo.GetTempaleTankGrading",
                          new
                          {
                              TankTypeId = tanktypeId,
                              ProjectId = projectId,
                              VesselType = vesseltype
                          },
                        commandType: CommandType.StoredProcedure,
                        transaction: Transaction);

                return result.ToList();

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<List<TankImageCard>> GetProjectTankImageCard(int projectId, int templateId, Guid sectionId)
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<TankImageCard>(SqliteProjectTankImageCard,
                    new { ProjectId = projectId, TemplateId = templateId, SectionId = sectionId }, Transaction);

            await using var connection = new SqlConnection(_unitOfWork.ConnectionString);

            await connection.OpenAsync();
            var result = await connection.QueryAsync<TankImageCard>(
                        sql: "dbo.GetTankImageCard",
                          new
                          {
                              ProjectId = projectId,
                              TemplateId = templateId,
                              SectionId = sectionId
                          },
                        commandType: CommandType.StoredProcedure);

            return result.ToList();

        }

        public async Task<TankImageCard> GetProjectTankImagCardByName(int projectId, int templateId, Guid sectionId, int cardNumber)
        {
            try
            {
                if (Connection.IsSqlite())
                {
                    var list = await Connection.QueryMappedAsync<TankImageCard>(
                        DigitalCap.Core.Constants.SQLiteQueries.Tank.GetProjectTankImageCardById,
                        new { ProjectId = projectId, TemplateId = templateId, SectionId = sectionId, CardNumber = cardNumber },
                        Transaction);
                    return list.FirstOrDefault()!;
                }

                var result = await Connection.QueryFirstOrDefaultAsync<TankImageCard>(
                    sql: "dbo.GetProjectTankImageCardByName",
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

                return result;
            }
            catch (Exception)
            {
                return null; // or throw/log based on your standard
            }
        }
        public async Task<bool> UpdateProjectTankImageCard(TankImageCard tankImageCard)
        {
            if (Connection.IsSqlite())
            {
                var p = new DynamicParameters();
                p.Add("@Id", tankImageCard.Id);
                p.Add("@ProjectId", tankImageCard.ProjectId);
                p.Add("@TemplateId", tankImageCard.TemplateId);
                p.Add("@SectionId", tankImageCard.SectionId);
                p.Add("@CardNumber", tankImageCard.CardNumber);
                p.Add("@DescriptionId", tankImageCard.DescriptionId);
                p.Add("@AdditionalDescription", tankImageCard.AdditionalDescription ?? "");
                p.Add("@CurrentCondition", tankImageCard.CurrentCondition);
                p.Add("@ImageId", tankImageCard.ImageId);
                p.Add("@CreatedDttm", tankImageCard.CreatedDttm == DateTime.MinValue ? DateTime.Now : tankImageCard.CreatedDttm);
                p.Add("@UpdatedDttm", DateTime.Now);
                p.Add("@IsActive", tankImageCard.IsActive ? 1 : 0);
                p.Add("@CardName", tankImageCard.CardName);

                var rows = await Connection.ExecuteAsync(
                    DigitalCap.Core.Constants.SQLiteQueries.Tank.UpdateProjectTankImageCard,
                    p,
                    Transaction);
                Commit();
                return rows > 0;
            }

            var affectedRows = await Connection.ExecuteAsync(
                sql: "dbo.UpdateTankImageCard",
                param: new
                {
                    tankImageCard.ProjectId,
                    tankImageCard.TemplateId,
                    tankImageCard.SectionId,
                    tankImageCard.CardNumber,
                    tankImageCard.CardName,
                    tankImageCard.DescriptionId,
                    tankImageCard.AdditionalDescription,
                    tankImageCard.CurrentCondition,
                    tankImageCard.IsActive
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return affectedRows > 0;
        }
        public async Task<bool> CreateProjectTankImageCard(TankImageCard tankImageCard)
        {
            if (Connection.IsSqlite())
            {
                var p = new DynamicParameters();
                p.Add("@ProjectId", tankImageCard.ProjectId);
                p.Add("@TemplateId", tankImageCard.TemplateId);
                p.Add("@SectionId", tankImageCard.SectionId);
                p.Add("@CardNumber", tankImageCard.CardNumber);
                p.Add("@DescriptionId", tankImageCard.DescriptionId);
                p.Add("@AdditionalDescription", tankImageCard.AdditionalDescription ?? "");
                p.Add("@CurrentCondition", tankImageCard.CurrentCondition);
                p.Add("@ImageId", tankImageCard.ImageId);
                p.Add("@CreatedDttm", tankImageCard.CreatedDttm == DateTime.MinValue ? DateTime.Now : tankImageCard.CreatedDttm);
                p.Add("@UpdatedDttm", tankImageCard.UpdatedDttm == DateTime.MinValue ? DateTime.Now : tankImageCard.UpdatedDttm);
                p.Add("@IsActive", tankImageCard.IsActive ? 1 : 0);
                p.Add("@CardName", tankImageCard.CardName);
                p.Add("@IsSync", 1);

                var rows = await Connection.ExecuteAsync(
                    DigitalCap.Core.Constants.SQLiteQueries.Tank.CreateProjectTankImageCard,
                    p,
                    Transaction);
                Commit();
                return rows > 0;
            }

            var affectedRows = await Connection.ExecuteAsync(
                sql: "dbo.CreateTankImageCard",
                param: new
                {
                    tankImageCard.ProjectId,
                    tankImageCard.TemplateId,
                    tankImageCard.SectionId,
                    tankImageCard.CardNumber,
                    tankImageCard.CardName,
                    tankImageCard.DescriptionId,
                    tankImageCard.AdditionalDescription,
                    tankImageCard.CurrentCondition,
                    tankImageCard.IsActive,
                    tankImageCard.ImageId,
                    // ❌ Id, src, IsSync intentionally excluded
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return affectedRows > 0;
        }
        public async Task<List<TankTypes>> GetTankTypes()
        {
            try
            {
                if (Connection is Microsoft.Data.Sqlite.SqliteConnection || Connection.GetType().Name.Contains("Sqlite"))
                {
                    var sqliteResult = await Connection.QueryAsync<TankTypes>(
                        sql: DigitalCap.Core.Constants.SQLiteQueries.Tank.GetTankType,
                        commandType: CommandType.Text,
                        transaction: Transaction);
                    return sqliteResult.ToList();
                }

                var result = await Connection.QueryAsync<TankTypes>(
                 sql: "dbo.GetTankType",
                 commandType: CommandType.StoredProcedure,
                 transaction: Transaction);

                return result.ToList();

            }
            catch (Exception ex)
            {
                return null;
            }
        }
        public async Task<List<TankTypes>> GetTankTypes(int projectId, string vesseltype)
        {
            try
            {
                if (Connection is Microsoft.Data.Sqlite.SqliteConnection || Connection.GetType().Name.Contains("Sqlite"))
                {
                    var sql = (projectId != 0)
                        ? DigitalCap.Core.Constants.SQLiteQueries.Tank.GetTankTypeByProjectTankMapping
                        : DigitalCap.Core.Constants.SQLiteQueries.Tank.GetTankTypeByVesselTankMapping;
                    var sqliteResult = await Connection.QueryAsync<TankTypes>(
                        sql: sql,
                        param: new { ProjectId = projectId, Vesseltype = vesseltype },
                        commandType: CommandType.Text,
                        transaction: Transaction);
                    return sqliteResult.ToList();
                }

                var result = await Connection.QueryAsync<TankTypes>(
                  sql: "[dbo].[GetTankTypeByTankMapping]",
                  new
                  {
                      ProjectId = projectId,
                      Veseltype = vesseltype
                  },
                 commandType: CommandType.StoredProcedure,
                 transaction: Transaction);
                return result.ToList();

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<List<VesselDetails>> GetVesselIMONo()
        {
            try
            {
                if (Connection is Microsoft.Data.Sqlite.SqliteConnection || Connection.GetType().Name.Contains("Sqlite"))
                {
                    var sqliteResult = await Connection.QueryAsync<VesselDetails>(
                        sql: DigitalCap.Core.Constants.SQLiteQueries.Tank.GetImoNumber,
                        commandType: CommandType.Text,
                        transaction: Transaction);
                    return sqliteResult.ToList();
                }

                var result = await Connection.QueryAsync<VesselDetails>(
                sql: "dbo.GetIMONumber",
                commandType: CommandType.StoredProcedure,
                transaction: Transaction);

                return result.ToList();

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<List<Core.Models.View.Admin.VesselTypes>> GetVesselType()
        {
            try
            {
                if (Connection is Microsoft.Data.Sqlite.SqliteConnection || Connection.GetType().Name.Contains("Sqlite"))
                {
                    var sqliteResult = await Connection.QueryAsync<Core.Models.View.Admin.VesselTypes>(
                        sql: "SELECT Id, VesselType AS Name FROM VesselType",
                        commandType: CommandType.Text,
                        transaction: Transaction);
                    return sqliteResult.ToList();
                }

                var result = await Connection.QueryAsync<Core.Models.View.Admin.VesselTypes>(
                    sql: "dbo.GetVesselType",
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction);

                return result.ToList();

            }
            catch (Exception ex)
            {
                return null;
            }
        }
        public async Task<List<Core.Models.View.Admin.Tank>> GetTanks_Vessel(string username = null)
        {
            try
            {
                if (Connection.IsSqlite())
                {
                    var sqliteResult = await Connection.QueryAsync<dynamic>(
                        sql: DigitalCap.Core.Constants.SQLiteQueries.Tank.GetTanksOfVessel,
                        param: new { username = username },
                        commandType: CommandType.Text,
                        transaction: Transaction);

                    var convertedResults = sqliteResult.Select(r => new Core.Models.View.Admin.Tank
                    {
                        TankId = r.TankId != null ? Guid.Parse(r.TankId.ToString()) : Guid.Empty,
                        VesselType = r.VesselType != null ? r.VesselType.ToString() : string.Empty,
                        TankName = r.TankName != null ? r.TankName.ToString() : string.Empty,
                        IsActive = Convert.ToBoolean(r.Status),
                        Status = Convert.ToBoolean(r.Status),
                        IMONumber = r.ImoNumber != null ? r.ImoNumber.ToString() : string.Empty,
                        VesselName = r.VesselName != null ? r.VesselName.ToString() : string.Empty,
                        TankType = r.TankType != null ? r.TankType.ToString() : string.Empty,
                        IsDeleted = r.IsDeleted != null ? Convert.ToBoolean(r.IsDeleted) : false,
                        ProjectName = r.ProjectName != null ? r.ProjectName.ToString() : string.Empty,
                        Subheader = r.Subheader != null ? r.Subheader.ToString() : string.Empty
                    });

                    return convertedResults.ToList();
                }

                var result = await Connection.QueryAsync<Core.Models.View.Admin.Tank>(
                    sql: "dbo.Get_Vessel_Tank",
                    param: new
                    {
                        username = username
                    },
                    commandType: CommandType.StoredProcedure,
                    commandTimeout: 300, // 5 minutes
                    transaction: Transaction
                );

                return result.ToList();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
        public async Task<List<Core.Models.View.Admin.Tank>> GetTanks_VesselByPagination(string username = null, int pageNumber = 1, int pageSize = 50)
        {
            try
            {
                var result = await Connection.QueryAsync<Core.Models.View.Admin.Tank>(
                    sql: "dbo.Get_Vessel_Tank_withPagination",
                    new
                    {
                        username = username,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction);

                return result.ToList();
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        public async Task<VesselTank> GetTanks_VesselById(Guid? Id, int? Projectid)
        {
            try
            {
                if (Connection.IsSqlite())
                {
                    var result = await Connection.QueryFirstOrDefaultAsync<VesselTank>(
                        sql: DigitalCap.Core.Constants.SQLiteQueries.Tank.GetTanksOfVesselById,
                        param: new { Id = Id?.ToString() },
                        transaction: Transaction);

                    if (result == null && Id.HasValue)
                    {
                        result = await Connection.QueryFirstOrDefaultAsync<VesselTank>(
                            sql: DigitalCap.Core.Constants.SQLiteQueries.Tank.GetTanksOfVesselById,
                            param: new { Id = Id.Value.ToString().ToUpper() },
                            transaction: Transaction);
                    }

                    return result;
                }

                return await GetTanks_VesselByIdSqlServer(Id, Projectid);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private async Task<VesselTank> GetTanks_VesselByIdSqlServer(Guid? Id, int? Projectid)
        {
            try
            {
                var result = await Connection.QueryAsync<VesselTank>(
                    sql: "dbo.Get_Vessel_Tank_ById",
                      new
                      {
                          Id = Id,
                          ProjectId = Projectid
                      },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction);

                return result.ToList().FirstOrDefault();

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<bool> UpdateTank(VesselTank vesselTank)
        {
            try
            {
                if (Connection.IsSqlite())
                {
                    var rows = await Connection.ExecuteAsync(
                        sql: DigitalCap.Core.Constants.SQLiteQueries.Tank.UpdateTankOfVessel,
                        param: new
                        {
                            Id = vesselTank.Id.ToString(),
                            VesselType = vesselTank.VesselType,
                            ImoNumber = vesselTank.ImoNumber,
                            TankTypeId = vesselTank.TankTypeId,
                            TankName = vesselTank.TankName,
                            IsActive = vesselTank.IsActive ? 1 : 0,
                            CreatedDttm = vesselTank.CreatedDttm == DateTime.MinValue ? DateTime.Now : vesselTank.CreatedDttm,
                            UpdateDttm = DateTime.Now,
                            IsDeleted = vesselTank.IsDeleted ? 1 : 0,
                            Subheader = vesselTank.Subheader
                        },
                        transaction: Transaction);

                    this.Commit();
                    return rows > 0;
                }

                return await UpdateTankSqlServer(vesselTank);
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task<bool> UpdateTankSqlServer(VesselTank vesselTank)
        {
            try
            {
                var rowsAffected = await Connection.ExecuteAsync(
                    "dbo.Update_Vessel_TankMapping",
                    new
                    {
                        Id = vesselTank.Id,
                        VesselType = vesselTank.VesselType,
                        ImoNumber = vesselTank.ImoNumber,
                        TankTypeId = vesselTank.TankTypeId,
                        TankName = vesselTank.TankName,
                        IsActive = vesselTank.IsActive,
                        UpdateDttm = vesselTank.UpdateDttm,
                        IsDeleted = vesselTank.IsDeleted,
                        Subheader = vesselTank.Subheader,
                        RequiredInReport = vesselTank.RequiredInReport,
                        ProjectId = vesselTank.ProjectId
                    },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                    );

                this.Commit();
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating vessel tank"); // optional but recommended
                return false;
            }
        }


        public async Task<List<VesselTankDetails>> GetVesselTypeList()
        {
            try
            {
                var result = await Connection.QueryAsync<VesselTankDetails>(
                    sql: "dbo.GetVesselTypeList",
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction);

                return result.ToList();

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<List<IMOTankFilterModel>> GetProjectNames(string imoNumber)
        {
            try
            {
                var result = await Connection.QueryAsync<IMOTankFilterModel>(
                    sql: "CAP.GET_PROJECTNAME_BYIMO",
                    new { IMO = imoNumber },
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction);
                return result.ToList();

            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public async Task<bool> UpdateStatus(List<Guid> tankIds, bool status)
        {
            if (tankIds == null || tankIds.Count == 0)
                return false;

            try
            {
                if (Connection.IsSqlite())
                {
                    int total = 0;
                    foreach (var id in tankIds)
                    {
                        total += await Connection.ExecuteAsync(
                            sql: DigitalCap.Core.Constants.SQLiteQueries.Tank.UpdateTankStatus,
                            param: new
                            {
                                Id = id.ToString(),
                                Status = status ? 1 : 0
                            },
                            transaction: Transaction);
                    }
                    this.Commit();
                    return total > 0;
                }

                return await UpdateStatusSqlServer(tankIds, status);
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        private async Task<bool> UpdateStatusSqlServer(List<Guid> tankIds, bool status)
        {
            if (tankIds == null || tankIds.Count == 0)
                return false;

            try
            {
                // Prepare TVP DataTable
                var tankIdTable = new DataTable();
                tankIdTable.Columns.Add("TankId", typeof(Guid));

                foreach (var id in tankIds)
                {
                    tankIdTable.Rows.Add(id);
                }

                var parameters = new DynamicParameters();
                parameters.Add(
                    "@TankIds",
                    tankIdTable.AsTableValuedParameter("dbo.TankIdTableType")
                );
                parameters.Add("@Status", status);

                var affectedRows = await Connection.ExecuteAsync(
                    sql: "dbo.UpdateTankStatus",
                    param: parameters,
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );

                Commit(); // commit only after successful execution
                return affectedRows > 0;
            }
            catch (Exception ex)
            {
                // TODO: log ex
                return false;
            }
        }

        public Task<IEnumerable<Core.Enumerations.ShipType>> GetShipTypes()
        {
            throw new NotImplementedException();
        }
    }
}
