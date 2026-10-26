using System;
using System.IO;

namespace DigitalCap.Core.Services
{
    public class OneDrivePathService
    {
        public string GetOneDriveRoot()
        {
            var oneDrivePath = Environment.GetEnvironmentVariable("OneDrive")
                            ?? Environment.GetEnvironmentVariable("OneDriveConsumer")
                            ?? Environment.GetEnvironmentVariable("OneDriveCommercial");

            if (string.IsNullOrEmpty(oneDrivePath) || !Directory.Exists(oneDrivePath))
            {
                // Fallback to LocalApplicationData if OneDrive is not available
                oneDrivePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ABS");
            }

            return oneDrivePath;
        }

        public string GetDatabaseDirectory()
        {
            var root = GetOneDriveRoot();
            var dbFolder = Path.Combine(root, AppKeyService.DatabaseFolder);
            if (!Directory.Exists(dbFolder))
            {
                Directory.CreateDirectory(dbFolder);
            }
            return dbFolder;
        }

        public string GetDatabasePath()
        {
            var dbFolder = GetDatabaseDirectory();
            return Path.Combine(dbFolder, AppKeyService.EncryptedDb);
        }

        public string GetDownloadDirectory()
        {
            var root = GetOneDriveRoot();
            var downloadFolder = Path.Combine(root, AppKeyService.DownloadDbFolder);
            if (!Directory.Exists(downloadFolder))
            {
                Directory.CreateDirectory(downloadFolder);
            }
            return downloadFolder;
        }
    }
}
