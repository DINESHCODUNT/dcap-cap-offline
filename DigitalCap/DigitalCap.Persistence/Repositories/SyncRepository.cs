using Dapper;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Models.Survey;
using DigitalCap.Core.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace DigitalCap.Persistence.Repositories
{
    public class SyncRepository : ISyncRepository
    {
        protected readonly IUnitOfWork _unitOfWork;
        private readonly bool _isElectron;
        protected IDbConnection Connection => _unitOfWork?.Connection!;
        protected IDbTransaction Transaction => _unitOfWork?.Transaction!;

        public SyncRepository(IUnitOfWork unitOfWork, IConfiguration configuration)
        {
            _unitOfWork = unitOfWork;
            _isElectron = configuration.GetValue<bool>("IsElectron", true);
        }

        public async Task<LoginData?> GetUserInfo()
        {
            const string sql = @"SELECT
                    Id,
                    FirstName,
                    LastName,
                    ProviderKey,
                    EmailId
               FROM LogInData";

            if (_isElectron)
            {
                using var local = OpenLocalDatabase();
                return await local.QueryFirstOrDefaultAsync<LoginData>(sql, commandType: CommandType.Text);
            }

            return await Connection.QueryFirstOrDefaultAsync<LoginData>(
                sql: sql,
                commandType: CommandType.Text,
                transaction: Transaction
            );
        }

        /// <summary>
        /// Records the signed-in user in the desktop's LogInData so the app can identify them offline.
        /// Matches digital-cap-offline's InsertLoginData: one row per user, existing rows are kept.
        /// </summary>
        public async Task SaveUserInfo(LoginData user)
        {
            if (!_isElectron)
                return;

            using var local = OpenLocalDatabase();
            await local.ExecuteAsync(
                sql: @"INSERT INTO LogInData (Id, FirstName, LastName, ProviderKey, EmailId)
                       SELECT @Id, @FirstName, @LastName, @ProviderKey, @EmailId
                       WHERE NOT EXISTS (SELECT 1 FROM LogInData WHERE lower(EmailId) = lower(@EmailId))",
                param: user,
                commandType: CommandType.Text
            );

            var displayName = $"{user.FirstName} {user.LastName}".Trim();
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = user.EmailId ?? string.Empty;

            await local.ExecuteAsync(
                sql: @"INSERT OR REPLACE INTO AspNetUserLogins (LoginProvider, ProviderKey, ProviderDisplayName, UserId)
                       VALUES ('AzureAD', COALESCE(@ProviderKey, @Id), @DisplayName, @Id);",
                param: new { user.ProviderKey, user.Id, DisplayName = displayName },
                commandType: CommandType.Text
            );
        }

        // LogInData describes who uses this desktop, so it always lives in the local database,
        // even while the unit of work is connected to Azure SQL for online work.
        private static SqliteConnection OpenLocalDatabase()
        {
            var dbPath = new OneDrivePathService().GetDatabasePath();
            var connection = new SqliteConnection($"Data Source={dbPath};Password={AppKeyService.GetDecryptedKey()};");
            connection.Open();
            return connection;
        }

    }
}
