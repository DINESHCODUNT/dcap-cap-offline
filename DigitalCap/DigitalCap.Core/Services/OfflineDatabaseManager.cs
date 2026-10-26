using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace DigitalCap.Core.Services
{
    public class OfflineDatabaseManager
    {
        private readonly OneDrivePathService _pathService;
        private readonly string _templateDbPath;
        private readonly string _encryptionKey;

        public OfflineDatabaseManager(string webRootPath)
        {
            _pathService = new OneDrivePathService();
            _templateDbPath = Path.Combine(webRootPath, AppKeyService.EncryptedDb);
            _encryptionKey = AppKeyService.GetDecryptedKey();
        }

        public string EnsureWorkingDatabaseExists()
        {
            var workingDbPath = _pathService.GetDatabasePath();

            if (!File.Exists(workingDbPath))
            {
                if (File.Exists(_templateDbPath))
                {
                    var dir = Path.GetDirectoryName(workingDbPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.Copy(_templateDbPath, workingDbPath, overwrite: true);
                }
                else
                {
                    return _templateDbPath;
                }
            }

            return workingDbPath;
        }

        public string GetEffectiveConnectionString()
        {
            var dbPath = EnsureWorkingDatabaseExists();
            if (File.Exists(dbPath))
            {
                return $"Data Source={dbPath};Password={_encryptionKey};";
            }
            if (File.Exists(_templateDbPath))
            {
                return $"Data Source={_templateDbPath};Password={_encryptionKey};";
            }
            return $"Data Source={dbPath};Password={_encryptionKey};";
        }
    }
}
