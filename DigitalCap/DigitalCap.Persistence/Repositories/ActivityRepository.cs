using Dapper;
using DapperExtensions;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Models;
using DigitalCap.Persistence.Extensions;
using Microsoft.AspNetCore.Connections;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace DigitalCap.Persistence.Repositories
{
    public class ActivityRepository: IActivityRepository
    {
        protected readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<ActivityRepository> _logger;
        protected IDbConnection Connection => _unitOfWork?.Connection!;
        protected IDbTransaction Transaction => _unitOfWork?.Transaction!;
        public ActivityRepository(IUnitOfWork unitOfWork, ILogger<ActivityRepository> logger) 
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }
        // Offline reads port SQLiteQueries.ReadActivities / ReadActivitiesByProjectId (reference
        // ActivityOfflineRepository); the project columns are LEFT JOINed so activities of released projects still show.
        private const string SqliteReadActivities = @"
            SELECT a.ID, a.Text, a.CreateBy, a.CreateDttm, a.ProjectId, p.PIDNumber, p.VesselName
            FROM Activities a LEFT JOIN Projects p ON a.ProjectId = p.ID
            WHERE COALESCE(a.IsDeleted, 0) = 0
            ORDER BY a.CreateDttm DESC LIMIT @Take OFFSET @Skip";

        private const string SqliteReadActivitiesByProjectId = @"
            SELECT a.ID, a.Text, a.CreateBy, a.CreateDttm, a.ProjectId, p.PIDNumber, p.VesselName
            FROM Activities a LEFT JOIN Projects p ON a.ProjectId = p.ID
            WHERE a.ProjectId = @ProjectId AND COALESCE(a.IsDeleted, 0) = 0
            ORDER BY a.CreateDttm DESC LIMIT @Take";

        private async Task<List<RecentActivity>> QuerySqliteActivities(string sql, object param, string what)
        {
            try
            {
                return await Connection.QueryMappedAsync<RecentActivity>(sql, param, Transaction);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching {What} from the local database", what);
                throw;
            }
        }

        public async Task<List<RecentActivity>> GetRecentActivitiesAsync(int take)
        {
            if (Connection.IsSqlite())
                return await QuerySqliteActivities(SqliteReadActivities, new { Take = take, Skip = 0 }, "recent activities");

            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@Take", take);

                var result = await Connection.QueryAsync<RecentActivity>(
                sql: "[CAP].[Read_Activities]",
                param: parameters,
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

                return result.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching recent activities");
                throw;
            }
        }


        public async Task<List<RecentActivity>> GetRecentActivitiesAsync2(int page, int size)
        {
            if (Connection.IsSqlite())
                return await QuerySqliteActivities(SqliteReadActivities, new { Take = size, Skip = page * size }, "paged recent activities");

            try
            {
                var sort = new List<ISort>
                {
                    Predicates.Sort<RecentActivity>(x => x.CreateDttm, false)
                };

                // Retrieve all activities without any date filtering
                var results = await Connection.GetPageAsync<RecentActivity>(
                    null, sort, page, size, transaction: Transaction);
                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error fetching paged recent activities");
                return new List<RecentActivity>();
            }
        }

        public async Task<List<RecentActivity>> GetRecentActivitiesByProjectIdAsync(int size, int projectId)
        {
            if (Connection.IsSqlite())
                return await QuerySqliteActivities(SqliteReadActivitiesByProjectId, new { Take = size, ProjectId = projectId }, "activities by projectId");

            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@Take", size);
                parameters.Add("@ProjectId", projectId);

                var result = await Connection.QueryAsync<RecentActivity>(
            sql: "[CAP].[Read_Activities_ByProjectId]",
            param: parameters,
            commandType: CommandType.StoredProcedure,
            transaction: Transaction
        );

                return result.AsList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching activities by projectId");
                throw;
            }
        }
    }
}
