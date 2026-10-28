using Dapper;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Models.DataTransfer;
using DigitalCap.Core.Models.Permissions;
using DigitalCap.Persistence.Extensions;
using DigitalCap.Core.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Transactions;

namespace DigitalCap.Persistence.Repositories
{
    public class UserPermissionRepository : IUserPermissionRepository
    {
        protected readonly IUnitOfWork _unitOfWork;
        protected IDbConnection Connection => _unitOfWork?.Connection!;
        protected IDbTransaction Transaction => _unitOfWork?.Transaction!;
        public UserPermissionRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }
        public void Commit()
        {
            _unitOfWork?.Commit();
        }
        public async Task<PermissionViewModel> GetRolePermissionByUserName(string username, string viewname, int? projectId)
        {
            // Capture connection and ensure it's ready
            var connection = _unitOfWork?.Connection;
            if (connection == null)
                throw new InvalidOperationException("Database connection is not initialized.");

            // Offline: evaluate the permissions stored at download (reference OfflineController.GetReportPermission).
            if (connection.IsSqlite())
                return await GetOfflineRolePermission(connection, username, viewname, projectId);

            // Ensure connection is open
            if (connection.State != ConnectionState.Open)
            {
                if (connection.State == ConnectionState.Closed)
                    connection.Open();
                else
                    throw new InvalidOperationException($"Database connection is in an invalid state: {connection.State}");
            }

            // Get transaction - ensure we have a valid one
            var transaction = _unitOfWork?.Transaction;
            
            // Check if we need to start a new transaction
            // Only call Begin() if transaction is null or disposed
            // Important: Don't call Begin() if transaction exists - UnitOfWork.Begin() doesn't check for existing transaction
            if (transaction == null)
            {
                // No transaction exists, start a new one
                _unitOfWork?.Begin();
                transaction = _unitOfWork?.Transaction;
                
                if (transaction == null)
                {
                    throw new InvalidOperationException("Failed to start a new transaction for read operation.");
                }
            }
            else if (transaction.Connection == null)
            {
                // Transaction exists but was disposed (e.g., after Commit)
                // We need to start a new one, but UnitOfWork.Begin() doesn't dispose existing transaction
                // So we need to handle this carefully
                _unitOfWork?.Begin();
                transaction = _unitOfWork?.Transaction;
                
                if (transaction == null)
                {
                    throw new InvalidOperationException("Failed to start a new transaction after previous one was disposed.");
                }
            }
            
            // Final check - ensure transaction is valid
            if (transaction.Connection == null || transaction.Connection.State != ConnectionState.Open)
            {
                throw new InvalidOperationException("Transaction is in an invalid state. Connection may have been closed or transaction disposed.");
            }

            // Final validation - ensure transaction is still valid
            if (transaction.Connection == null || transaction.Connection.State != ConnectionState.Open)
            {
                throw new InvalidOperationException("Transaction is in an invalid state after initialization.");
            }

            try
            {
                // Use DynamicParameters for better compatibility
                var parameters = new DynamicParameters();
                parameters.Add("@username", username, DbType.String);
                parameters.Add("@viewname", viewname, DbType.String);
                parameters.Add("@projectId", projectId, DbType.Int32);

                // Execute query with validated transaction
                var result = await connection.QueryFirstOrDefaultAsync<PermissionViewModel>(
                    sql: "dbo.GetCurrentUserPermission",
                    param: parameters,
                    commandType: CommandType.StoredProcedure,
                    transaction: transaction,
                    commandTimeout: 120
                );

                return result;
            }
            catch (Exception ex) when (
                ex.Message.Contains("ReadAsync when reader is closed") ||
                ex.Message.Contains("MetaData when reader is closed"))
            {
                // Reader was closed - this happens when transaction is disposed during query
                // This is a critical error that shouldn't happen, but if it does, 
                // we need to ensure transaction state is correct
                throw new InvalidOperationException(
                    $"Database reader was closed during query execution. This may indicate a transaction management issue. " +
                    $"Error: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex.Message.Contains("pending local transaction"))
            {
                // Connection has pending transaction but we didn't pass it correctly
                throw new InvalidOperationException(
                    $"Connection has pending transaction but transaction parameter was invalid. " +
                    $"Error: {ex.Message}", ex);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<List<int>> GetCurrentUserProjects(string username)
        {
            // Offline: the projects on this computer were downloaded by a user allowed to open them.
            if (Connection.IsSqlite())
                return (await Connection.QueryAsync<int>("SELECT ID FROM Projects ORDER BY ID", transaction: Transaction)).ToList();

            var result = await Connection.QueryAsync<int>(
                sql: "dbo.GetCurrentUserProjects",
                param: new { username },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result.ToList();
        }

        public async Task<List<UserPermission>> GetAllPermissionsAsync()
        {
            var result = await Connection.QueryAsync<UserPermission>(
                sql: "dbo.GetUserPermissions",
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result.ToList();
        }

        public async Task<bool> AddPermissionAsync(UserPermission permission)
        {
            var result = await Connection.ExecuteAsync(
                sql: "dbo.Insert_UserPermissions",
                param: new
                {
                    Id = permission.Id,
                    PermissionName = permission.PermissionName
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );
            this.Commit();
            return result > 0;
        }
        public async Task<bool> UpdateRolePermissionAsync(UserRolePermission rolepermission)
        {
            if (rolepermission == null)
                throw new ArgumentNullException(nameof(rolepermission));

            var result = await Connection.ExecuteAsync(
                sql: "dbo.Update_RolePermissionMapping",
                param: new
                {
                    Id = rolepermission.Id,
                    RoleId = rolepermission.RoleId,
                    PermissionId = rolepermission.PermissionId,
                    read = rolepermission.IsReadable,
                    edit = rolepermission.IsEditable,
                    delete = rolepermission.IsDeleted
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );
            this.Commit();
            return result > 0;
        }
        public async Task<bool> AddRolePermissionAsync(UserRolePermission rolepermission)
        {
            if (rolepermission == null)
                throw new ArgumentNullException(nameof(rolepermission));

            var result = await Connection.ExecuteAsync(
                sql: "dbo.Insert_RolePermissionMapping",
                param: new
                {
                    Id = rolepermission.Id,
                    RoleId = rolepermission.RoleId,
                    PermissionId = rolepermission.PermissionId,
                    read = rolepermission.IsReadable,
                    edit = rolepermission.IsEditable,
                    delete = rolepermission.IsDeleted
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );
            this.Commit();
            return result > 0;
        }
        public async Task<bool> DeleteRolePermissionAsync(Guid id)
        {
            var result = await Connection.ExecuteAsync(
                sql: "dbo.Delete_RolePermissionMapping",
                param: new
                {
                    RoleId = id
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );
            this.Commit();
            return result > 0;
        }
        public async Task<bool> UpdatePermissionAsync(UserPermission permission)
        {
            if (permission == null)
                throw new ArgumentNullException(nameof(permission));

            var result = await Connection.ExecuteAsync(
                sql: "dbo.Update_UserPermissions",
                param: new
                {
                    Id = permission.Id,
                    PermissionName = permission.PermissionName
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );
            this.Commit();
            return result > 0;
        }
        public async Task<bool> DeletePermissionAsync(Guid id)
        {
            var result = await Connection.ExecuteAsync(
                sql: "dbo.Delete_UserPermissions",
                param: new
                {
                    Id = id
                },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );
            this.Commit();
            return result > 0;
        }
        public async Task<UserRolePermission> GetRolePermissionById(string id)
        {
            var result = await Connection.QueryFirstOrDefaultAsync<UserRolePermission>(
                sql: "dbo.GetRolePermissionById",
                param: new { Id = id },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result;
        }
        public async Task<List<Role>> GetAllRoles()
        {
            try
            {
                if (Connection.IsSqlite())
                {
                    // Offline: the role names of the permission matrix stored at download (no AspNetRoles ids locally).
                    var names = await Connection.QueryAsync<string>(
                        "SELECT DISTINCT RoleName FROM OfflineRolePermissions ORDER BY RoleName", transaction: Transaction);
                    return names.Select(n => new Role { Id = n, Name = n }).ToList();
                }

                var result = await Connection.QueryAsync<Role>(
                    sql: "dbo.GetAspNetUserRoles",
                    commandType: CommandType.StoredProcedure,
                    transaction: Transaction
                );

                return result.ToList();
            }
            catch (Exception)
            {
                return new List<Role>();
            }
        }
        public async Task<List<UserRolePermissionViewModel>> GetRolePermission(string id)
        {
            var result = await Connection.QueryAsync<UserRolePermissionViewModel>(
                sql: "dbo.GetUserRolePermission",
                param: new { Id = id },
                commandType: CommandType.StoredProcedure,
                transaction: Transaction
            );

            return result.ToList();
        }

        // ------------------------------------------------------------------ offline (local database)

        /// <summary>
        /// Roles that apply to the user for a project, as the reference evaluated them offline
        /// (OfflineController.GetReportPermission / GetManageTankOfflinePermission): the user's stored roles plus
        /// "CAP Lead" when the user is the project's LeadSurveyor and "CAP Team" when listed in SecondSurveyor.
        /// </summary>
        public const string CapLeadRole = "CAP Lead";
        public const string CapTeamRole = "CAP Team";

        private async Task<PermissionViewModel> GetOfflineRolePermission(IDbConnection connection, string username, string viewname, int? projectId)
        {
            var user = username?.Trim() ?? string.Empty;
            var stored = await connection.ExecuteScalarAsync<long>(
                "SELECT COUNT(1) FROM OfflineUserSecurity WHERE UserName = @UserName", new { UserName = user }, Transaction);
            if (stored == 0)
                return null!; // nothing downloaded by this user yet: same as "no permission row" online

            var roles = (await connection.QueryAsync<string>(
                "SELECT RoleName FROM OfflineUserRoles WHERE UserName = @UserName", new { UserName = user }, Transaction)).ToList();

            if (projectId.HasValue)
            {
                var project = (await connection.QueryAsync(
                    "SELECT LeadSurveyor, SecondSurveyor FROM Projects WHERE ID = @ID", new { ID = projectId.Value }, Transaction))
                    .Cast<IDictionary<string, object>>().FirstOrDefault();
                if (project != null)
                {
                    var lead = project["LeadSurveyor"] as string;
                    var team = (project["SecondSurveyor"] as string)?.Split(',') ?? Array.Empty<string>();
                    if (IsSameUser(lead, user))
                        roles.Add(CapLeadRole);
                    if (team.Any(t => IsSameUser(t, user)))
                        roles.Add(CapTeamRole);
                }
            }

            var result = new PermissionViewModel { PermissionName = viewname };
            if (roles.Count == 0)
                return result;

            var rows = await connection.QueryMappedAsync<OfflineRolePermission>(
                "SELECT RoleName, PermissionName, [Read], [Edit], [Delete] FROM OfflineRolePermissions WHERE PermissionName = @PermissionName AND RoleName IN @Roles",
                new { PermissionName = viewname, Roles = roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList() }, Transaction);
            foreach (var row in rows)
            {
                result.Read |= row.Read;
                result.Edit |= row.Edit;
                result.Delete |= row.Delete;
            }
            return result;
        }

        private static bool IsSameUser(string? surveyor, string user)
            => !string.IsNullOrWhiteSpace(surveyor) && string.Equals(surveyor.Trim(), user, StringComparison.OrdinalIgnoreCase);

        public async Task<OfflineUserSecurity?> GetOfflineUserSecurityAsync(string userName)
        {
            if (!Connection.IsSqlite() || string.IsNullOrWhiteSpace(userName))
                return null;

            var user = new { UserName = userName.Trim() };
            var header = await Connection.QueryFirstMappedAsync<OfflineUserSecurity>(
                "SELECT UserName, AspNetUserId FROM OfflineUserSecurity WHERE UserName = @UserName", user, Transaction);
            if (header == null)
                return null;

            header.Roles = (await Connection.QueryAsync<string>(
                "SELECT RoleName FROM OfflineUserRoles WHERE UserName = @UserName ORDER BY RoleName", user, Transaction)).ToList();
            header.Claims = (await Connection.QueryAsync<string>(
                "SELECT ClaimType FROM OfflineUserClaims WHERE UserName = @UserName", user, Transaction)).ToList();
            return header;
        }
    }
}
