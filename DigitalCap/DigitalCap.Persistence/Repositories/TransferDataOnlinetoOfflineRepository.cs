using Dapper;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Models;
using DigitalCap.Core.Models.Survey;
using DigitalCap.Persistence.Extensions;
using DigitalCap.WebApi.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace DigitalCap.Persistence.Repositories
{
    public class TransferDataOnlinetoOfflineRepository : RepositoryBase<UserAccountModel, Guid>, ITransferDataOnlinetoOfflineRepository
    {
        private readonly ILogger<TransferDataOnlinetoOfflineRepository> _logger;
        private readonly IHttpContextAccessor _httpContext;
        private readonly UserManager<ApplicationUser> _userManager;
        public TransferDataOnlinetoOfflineRepository(IUnitOfWork unitOfWork, ILogger<TransferDataOnlinetoOfflineRepository> logger) : base(unitOfWork, logger)
        {
            _logger = logger;
        }

        public async Task<IsSynchedOnline> GetDownloadOfflineProjects(int projectId)
        {
            // The download marker exists only online (dbo.GetDownloadOfflineProject); offline there is none to show.
            if (Connection.IsSqlite())
                return null!;

            var parameters = new DynamicParameters();
            parameters.Add("@ProjectId", projectId);

            var result = await Connection.QueryFirstOrDefaultAsync<IsSynchedOnline>(
                sql: "dbo.GetDownloadOfflineProject",
                parameters,
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result;
        }

        public async Task<string> GetOfflineUserRole(string Id)
        {
            var parameters = new DynamicParameters();
            parameters.Add("@Id", Id);

            var result = await Connection.QueryFirstOrDefaultAsync<string>(
                sql: "[dbo].GetOfflineUserRole",
                parameters,
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result;
        }

        public async Task<bool> ReleaseReadOnlyOnline(int projectId, string userName)
        {
            try
            {
                var parameters = new DynamicParameters();
                parameters.Add("@ProjectId", projectId);
                parameters.Add("@UserName", userName);

                var result = await Connection.QueryAsync(
                    sql: "dbo.ReadOnlyData",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in ReleaseReadOnlyOnline");
                return false;
            }
        }
    }
}
