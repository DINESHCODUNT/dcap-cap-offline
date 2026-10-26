using Microsoft.Data.Sqlite;

namespace DigitalCap.Core.Services
{
    public class EncryptionDatabaseService
    {
        private readonly string _dbPath;
        private readonly string _password;

        public EncryptionDatabaseService(string dbPath, string password)
        {
            _dbPath = dbPath;
            _password = password;
        }

        public SqliteConnection GetConnection()
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _dbPath,
                Password = _password,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            return new SqliteConnection(connectionString);
        }

        public void EncryptExistingDatabase(string plainDbPath, string encryptedDbPath)
        {
            using var plainConn = new SqliteConnection($"Data Source={plainDbPath}");
            plainConn.Open();

            using var cmd = plainConn.CreateCommand();
            cmd.CommandText = $@"
                ATTACH DATABASE '{encryptedDbPath}' AS encrypted KEY '{_password}';
                SELECT sqlcipher_export('encrypted');
                DETACH DATABASE encrypted;
            ";
            cmd.ExecuteNonQuery();
            plainConn.Close();
        }
    }
}
