using DigitalCap.Core.Interfaces.Repository;
using Microsoft.Data.Sqlite;

namespace DigitalCap.Core.Helpers.Extensions
{
    public static class UnitOfWorkExtensions
    {
        /// <summary>
        /// True when the unit of work is connected to the local SQLCipher database (desktop app offline, or
        /// online with Azure SQL unreachable). Services use it to skip work that only exists online.
        /// Repositories use DigitalCap.Persistence.Extensions.ConnectionExtensions.IsSqlite on their connection.
        /// </summary>
        public static bool UsesLocalDatabase(this IUnitOfWork? unitOfWork) => unitOfWork?.Connection is SqliteConnection;
    }
}
