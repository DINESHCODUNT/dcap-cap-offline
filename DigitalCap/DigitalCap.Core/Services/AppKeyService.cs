using System;
using System.Security.Cryptography;
using System.Text;

namespace DigitalCap.Core.Services
{
    public static class AppKeyService
    {
        private const string MasterSecret = "DCAP#UAT$2026!@XyZ#Master#Secret";
        private const string Salt = "DCAP-UAT-SALT-2026";

        public const string DatabaseFolder = "digital-cap-db";
        public const string EncryptedDb = "sqldb-cap-dev-001.db.enc";
        public const string DownloadDbFolder = "digital-cap";

        // Encrypted DB key
        private const string EncryptedDatabaseKey =
            "vxI1g/JLewFiv8sFqT6HaySFXaEasYNaFjHrOcamE6u9fr5eumFE08sHd9wGahbQ22Qo2FvVDjTQH1MWEgznSA==";

        /// <summary>
        /// Returns the decrypted key. This is the only public entry point needed at runtime.
        /// </summary>
        public static string GetDecryptedKey()
        {
            return DecryptKey(EncryptedDatabaseKey);
        }

        internal static string EncryptKey(string plainKey)
        {
            using var aes = Aes.Create();
            aes.Key = DeriveKey();
            aes.GenerateIV();

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainKey);
            var encrypted = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            var result = new byte[aes.IV.Length + encrypted.Length];
            aes.IV.CopyTo(result, 0);
            encrypted.CopyTo(result, aes.IV.Length);

            return Convert.ToBase64String(result);
        }

        private static string DecryptKey(string encryptedKey)
        {
            try
            {
                var fullBytes = Convert.FromBase64String(encryptedKey);

                using var aes = Aes.Create();
                aes.Key = DeriveKey();

                var iv = new byte[16];
                var encrypted = new byte[fullBytes.Length - 16];
                Array.Copy(fullBytes, 0, iv, 0, 16);
                Array.Copy(fullBytes, 16, encrypted, 0, encrypted.Length);

                aes.IV = iv;
                using var decryptor = aes.CreateDecryptor();
                var decrypted = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);

                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                // Fallback to default UAT key if master derivation differs
                return "DCAP@UAT2026#AbsKey!";
            }
        }

        private static byte[] DeriveKey()
        {
            using var deriveBytes = new Rfc2898DeriveBytes(
                MasterSecret,
                Encoding.UTF8.GetBytes(Salt),
                iterations: 10000,
                HashAlgorithmName.SHA256);

            return deriveBytes.GetBytes(32);
        }
    }
}
