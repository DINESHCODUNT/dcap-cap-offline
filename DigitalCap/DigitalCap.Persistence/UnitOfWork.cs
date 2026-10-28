using System;
using System.Data;
using System.IO;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Services;
using DigitalCap.Infrastructure.Service;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

namespace DigitalCap.Persistence
{
    public class UnitOfWork : IUnitOfWork, IDisposable
    {
        private readonly string _connectionString;
        private readonly bool _isElectron;
        private bool _disposed;

        public IDbConnection Connection { get; private set; }
        public IDbTransaction Transaction { get; private set; }
        public string ConnectionString => _connectionString;

        public UnitOfWork(IConfiguration configuration)
        {
            _isElectron = configuration.GetValue<bool>("IsElectron", true);
            var sqlServerConn = configuration.GetConnectionString("DefaultConnection")
                ?? configuration.GetConnectionString("ABSConnectionString") ?? "";

            bool isOnline = IsOnline.OnlineOrOffline;

            if (_isElectron)
            {
                try
                {
                    SQLitePCL.Batteries_V2.Init();
                }
                catch
                {
                    // Ignore if already initialized
                }

                var pathService = new OneDrivePathService();
                var oneDriveDb = pathService.GetDatabasePath();
                var decryptedKey = AppKeyService.GetDecryptedKey();
                string sqliteConnStr;

                if (File.Exists(oneDriveDb))
                {
                    sqliteConnStr = $"Data Source={oneDriveDb};Password={decryptedKey};";
                }
                else
                {
                    var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
                    var templateDb = Path.Combine(webRoot, AppKeyService.EncryptedDb);

                    if (File.Exists(templateDb))
                    {
                        var dir = Path.GetDirectoryName(oneDriveDb);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        try
                        {
                            File.Copy(templateDb, oneDriveDb, overwrite: true);
                            sqliteConnStr = $"Data Source={oneDriveDb};Password={decryptedKey};";
                        }
                        catch
                        {
                            sqliteConnStr = $"Data Source={templateDb};Password={decryptedKey};";
                        }
                    }
                    else
                    {
                        sqliteConnStr = $"Data Source={oneDriveDb};Password={decryptedKey};";
                    }
                }

                // Desktop (Electron) mode always uses the local SQLCipher SQLite database
                Connection = new SqliteConnection(sqliteConnStr);
                _connectionString = sqliteConnStr;
            }
            else
            {
                _connectionString = sqlServerConn;
                Connection = new SqlConnection(_connectionString);
            }
        }

        public void Begin()
        {
            if (Connection.State != ConnectionState.Open)
                Connection.Open();

            if (Transaction != null)
            {
                try
                {
                    Transaction.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
                Transaction = null;
            }

            Transaction = Connection.BeginTransaction();
        }

        public void Commit()
        {
            try
            {
                Transaction?.Commit();
            }
            finally
            {
                Transaction?.Dispose();
                Transaction = null;
            }
        }

        public void Rollback()
        {
            try
            {
                Transaction?.Rollback();
            }
            finally
            {
                Transaction?.Dispose();
                Transaction = null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Transaction?.Dispose();
                Connection?.Dispose();
                _disposed = true;
            }
        }
    }
}
