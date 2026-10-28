using Dapper;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Models.ReportConfig;
using DigitalCap.Core.Models.Survey;
using DigitalCap.Persistence.Extensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace DigitalCap.Persistence.Repositories
{
    public class ProjectReportRepository : IProjectReportRepository
    {
        protected readonly IUnitOfWork _unitOfWork;
        protected IDbConnection Connection => _unitOfWork?.Connection!;
        protected IDbTransaction Transaction => _unitOfWork?.Transaction!;
        public ProjectReportRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;            
        }
        public void Commit()
        {
            _unitOfWork?.Commit();
        }
        public async Task<GenericImageCard> GetGenericImagCardByName(int projectId, int templateId, Guid sectionId, int cardNumber)
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
                      new
                      {
                          ProjectId = projectId,
                          TemplateId = templateId,
                          SectionId = sectionId,
                          CardNumber = cardNumber
                      },
                    commandType: CommandType.StoredProcedure);

                return result.ToList().FirstOrDefault();

            }
            catch (Exception ex)
            {
                return null;
            }

        }

        public async Task<bool> UpdateGenericImageCard(GenericImageCard model)
        {
            try
            {
                if (Connection.IsSqlite())
                {
                    var p = new DynamicParameters();
                    p.Add("@Id", model.Id);
                    p.Add("@ProjectId", model.ProjectId);
                    p.Add("@TemplateId", model.TemplateId);
                    p.Add("@SectionId", model.SectionId);
                    p.Add("@CardNumber", model.CardNumber);
                    p.Add("@DescriptionId", model.DescriptionId);
                    p.Add("@AdditionalDescription", model.AdditionalDescription ?? "");
                    p.Add("@CurrentCondition", model.CurrentCondition);
                    p.Add("@ImageId", model.ImageId);
                    p.Add("@CreatedDttm", model.CreatedDttm == DateTime.MinValue ? DateTime.Now : model.CreatedDttm);
                    p.Add("@UpdatedDttm", DateTime.Now);
                    p.Add("@IsDeleted", model.IsDeleted ? 1 : 0);
                    p.Add("@IsActive", model.IsActive ? 1 : 0);
                    p.Add("@CardName", model.CardName);

                    var rows = await Connection.ExecuteAsync(
                        DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.UpdateGenericImageCard,
                        p,
                        Transaction);
                    Commit();
                    return rows > 0;
                }

                var parameters = new DynamicParameters();

                foreach (var prop in typeof(GenericImageCard).GetProperties())
                {
                    if (prop.Name == "src" || prop.Name == "IsSync")
                        continue;

                    parameters.Add(
                        name: "@" + prop.Name,
                        value: prop.GetValue(model)
                    );
                }

                await Connection.ExecuteAsync(
                    sql: "[config].[UpdateGenericImageCard]",
                    param: parameters,
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );

                return true;
            }
            catch
            {
                return false;
            }
        }
        public async Task<bool> CreateGenericImageCard(GenericImageCard model)
        {
            try
            {
                if (Connection.IsSqlite())
                {
                    var p = new DynamicParameters();
                    p.Add("@ProjectId", model.ProjectId);
                    p.Add("@TemplateId", model.TemplateId);
                    p.Add("@SectionId", model.SectionId);
                    p.Add("@CardNumber", model.CardNumber);
                    p.Add("@DescriptionId", model.DescriptionId);
                    p.Add("@AdditionalDescription", model.AdditionalDescription ?? "");
                    p.Add("@CurrentCondition", model.CurrentCondition);
                    p.Add("@ImageId", model.ImageId);
                    p.Add("@CreatedDttm", model.CreatedDttm == DateTime.MinValue ? DateTime.Now : model.CreatedDttm);
                    p.Add("@UpdatedDttm", model.UpdatedDttm == DateTime.MinValue ? DateTime.Now : model.UpdatedDttm);
                    p.Add("@IsActive", model.IsActive ? 1 : 0);
                    p.Add("@CardName", model.CardName);

                    var rows = await Connection.ExecuteAsync(
                        DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.CreateGenericImageCard,
                        p,
                        Transaction);
                    Commit();
                    return rows > 0;
                }

                var parameters = new DynamicParameters();

                foreach (var prop in typeof(GenericImageCard).GetProperties())
                {
                    if (prop.Name == "Id" ||
                        prop.Name == "src" ||
                        prop.Name == "IsSync")
                    {
                        continue;
                    }

                    parameters.Add(
                        name: "@" + prop.Name,
                        value: prop.GetValue(model)
                    );
                }

                await Connection.ExecuteAsync(
                    sql: "[config].[CreateGenericImageCard]",
                    param: parameters,
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<List<ProjectPart>> GetProjectTemplatebyProjectId(int projectId)
        {
            // Offline: the downloaded ReportPart rows plus the fixed Vessel Main Data (1) and Part H&I (2) parts.
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ProjectPart>(
                    DigitalCap.Core.Constants.SQLiteQueries.ProjectReport.GetReportPartsByProjectId,
                    new { projectId }, Transaction);

            var result = await Connection.QueryAsync<ProjectPart>(
            sql: "[Config].[GetReportPartsByProjectId]",
            param: new { projectId },
            commandType: CommandType.StoredProcedure,
            transaction: Transaction);

            return result.ToList();

        }

    }
}
