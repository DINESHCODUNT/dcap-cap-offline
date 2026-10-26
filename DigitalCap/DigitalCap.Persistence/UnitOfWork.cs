using System;
using System.Data;
using System.IO;
using DigitalCap.Core.Interfaces.Repository;
using DigitalCap.Core.Services;
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

                if (File.Exists(oneDriveDb))
                {
                    _connectionString = $"Data Source={oneDriveDb};Password={decryptedKey};";
                }
                else
                {
                    var webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
                    var templateDb = Path.Combine(webRoot, AppKeyService.EncryptedDb);

                    if (File.Exists(templateDb))
                    {
                        // Copy template to OneDrive working directory
                        var dir = Path.GetDirectoryName(oneDriveDb);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        {
                            Directory.CreateDirectory(dir);
                        }
                        try
                        {
                            File.Copy(templateDb, oneDriveDb, overwrite: true);
                            _connectionString = $"Data Source={oneDriveDb};Password={decryptedKey};";
                        }
                        catch
                        {
                            _connectionString = $"Data Source={templateDb};Password={decryptedKey};";
                        }
                    }
                    else
                    {
                        _connectionString = $"Data Source={oneDriveDb};Password={decryptedKey};";
                    }
                }

                Connection = new SqliteConnection(_connectionString);
            }
            else
            {
                _connectionString = configuration.GetConnectionString("DefaultConnection") ?? "";
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
