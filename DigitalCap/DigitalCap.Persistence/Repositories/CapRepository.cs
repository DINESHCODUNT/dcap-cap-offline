using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Constants;
using DigitalCap.Core.Models;
using DigitalCap.Persistence.Extensions;
using System.Data;
using Dapper;
using Newtonsoft.Json;

namespace DigitalCap.Persistence.Repositories
{
    public class CapRepository : ICapRepository
    {
        protected readonly IUnitOfWork _unitOfWork;
        protected IDbConnection Connection => _unitOfWork?.Connection!;
        protected IDbTransaction Transaction => _unitOfWork?.Transaction!;
        public CapRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }
        public void Commit()
        {
            _unitOfWork?.Commit();
        }
        
        // ------------------------------------------------------------------ offline (local SQLite)
        // The chart queries are the reference's SQLiteQueries (CapOfflineRepository), which return one JSON cell.

        private const string SqliteCapIndex = @"
            SELECT p.ID, p.Name, s.[Status] AS ProjectStatusName, p.CapRegion, p.IMO, p.VesselName, p.PIDNumber,
                   p.ProjectPriority, p.CompanyName, p.WCN
            FROM Projects p LEFT JOIN Statuses s ON s.ID = p.ProjectStatus
            ORDER BY p.ID DESC";

        private async Task<List<T>> QuerySqliteJson<T>(string sql)
        {
            var json = (await Connection.ExecuteScalarAsync(sql, transaction: Transaction))?.ToString();
            if (string.IsNullOrWhiteSpace(json))
                return new List<T>();
            return JsonConvert.DeserializeObject<List<T>>(json) ?? new List<T>();
        }

        public async Task<List<ProjectsGrid>> GetCapIndexAsync()
        {
            if (Connection.IsSqlite())
                return await Connection.QueryMappedAsync<ProjectsGrid>(SqliteCapIndex, transaction: Transaction);

            try
            {
                var result = await Connection.QueryAsync<ProjectsGrid>(
                sql: "[CAP].[Read_Project]",
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

                return result.AsList();
            }
            catch (Exception sqlEx)
            {
                throw;
            }
        }

        public async Task<List<ProjectCharts>> GetProjectByStatusesAsync()
        {
            if (Connection.IsSqlite())
                return await QuerySqliteJson<ProjectCharts>(SQLiteQueries.Project.ByStatus);

            var result = await Connection.QueryAsync<ProjectCharts>(
            sql : "[CAP].[Read_Project_Statuses]",
            commandType: CommandType.StoredProcedure,
            transaction: Transaction
        );

            return result.ToList();
        }

        

        public async Task<List<ProjectCharts>> GetCertsByRegionAsync()
        {
            try
            {
                var list = Connection.IsSqlite()
                    ? await QuerySqliteJson<ProjectCharts>(SQLiteQueries.ReadCertsByRegion)
                    : (await Connection.QueryAsync<ProjectCharts>(
                        sql: "[CAP].[Read_Certs_ByRegion]",
                        commandType: CommandType.StoredProcedure,
                        transaction: Transaction)).ToList();

                // Preserve original empty logic
                if (list == null || !list.Any())
                {
                    return new List<ProjectCharts>
                    {
                        new ProjectCharts
                        {
                        Category = "",
                        Value = 1,
                        Color = "rgb(219,220,198)"
                        }
                    };
                }

                return list;
            }
            catch (Exception)
            {
                return new List<ProjectCharts>();
            }
        }


        /// <summary>
        /// Matches legacy MVC: SP returns a single cell containing JSON for <see cref="ProjectChartsCount"/>.
        /// If your proc instead returns a rowset with a Count column, use QueryAsync&lt;ProjectChartsCount&gt; instead.
        /// </summary>
        public async Task<List<ProjectChartsCount>> GetProjectCountAsync()
        {
            if (Connection.IsSqlite())
                return await QuerySqliteJson<ProjectChartsCount>(SQLiteQueries.Project.Count);

            try
            {
                var scalar = await Connection.ExecuteScalarAsync(
                    "[CAP].[Read_Projects_Count]",
                    transaction: Transaction,
                    commandType: CommandType.StoredProcedure);

                var jsonString = scalar?.ToString();
                if (string.IsNullOrWhiteSpace(jsonString))
                    return new List<ProjectChartsCount>();

                var list = JsonConvert.DeserializeObject<List<ProjectChartsCount>>(jsonString);
                return list ?? new List<ProjectChartsCount>();
            }
            catch (Exception)
            {
                return new List<ProjectChartsCount>();
            }
        }


        public async Task<List<ProjectChartsCount>> GetCertsCountAsync()
        {
            if (Connection.IsSqlite())
                return await QuerySqliteJson<ProjectChartsCount>(SQLiteQueries.ReadCertsCount);

            try
            {
                var result = await Connection.QueryAsync<ProjectChartsCount>(
                sql: "[CAP].[Read_Certs_Count]",
                commandType: CommandType.StoredProcedure,
                transaction: Transaction);

                return result.AsList();
            }
            catch (Exception)
            {
                return new List<ProjectChartsCount>();
            }
        }

    }

}
