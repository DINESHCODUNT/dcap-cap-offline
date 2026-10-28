using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DigitalCap.Persistence.Extensions
{
    /// <summary>
    /// Helpers for repositories that run against either Azure SQL (online) or the local SQLCipher database
    /// (desktop offline). Repositories branch on <see cref="IsSqlite"/> and keep their SQL Server code untouched.
    /// </summary>
    public static class ConnectionExtensions
    {
        /// <summary>True when the unit of work is using the local SQLite (SQLCipher) database.</summary>
        public static bool IsSqlite(this IDbConnection? connection) => connection is SqliteConnection;

        /// <summary>
        /// Runs a SQLite query and maps each row onto <typeparamref name="T"/> by column name (case-insensitive).
        /// SQLite returns dates as TEXT, GUIDs as TEXT and every integer as Int64, which Dapper cannot always convert
        /// into the SQL Server-shaped models (Guid, DateTimeOffset, byte, enums, string[]); this conversion handles
        /// those. NULL leaves the property's default. Columns without a matching writable property are ignored.
        /// </summary>
        public static async Task<List<T>> QueryMappedAsync<T>(this IDbConnection connection, string sql, object? param = null, IDbTransaction? transaction = null)
            where T : new()
        {
            var rows = await connection.QueryAsync(sql, param, transaction, commandType: CommandType.Text);
            var props = WritableProperties(typeof(T));
            var result = new List<T>();
            foreach (IDictionary<string, object> row in rows)
                result.Add(MapRow<T>(row, props));
            return result;
        }

        public static async Task<T?> QueryFirstMappedAsync<T>(this IDbConnection connection, string sql, object? param = null, IDbTransaction? transaction = null)
            where T : class, new()
            => (await connection.QueryMappedAsync<T>(sql, param, transaction)).FirstOrDefault();

        /// <summary>Maps one Dapper dynamic row (IDictionary) onto a new <typeparamref name="T"/>.</summary>
        public static T MapRow<T>(IDictionary<string, object> row) where T : new()
            => MapRow<T>(row, WritableProperties(typeof(T)));

        private static T MapRow<T>(IDictionary<string, object> row, IReadOnlyDictionary<string, PropertyInfo> props) where T : new()
        {
            var item = new T();
            foreach (var (column, value) in row)
            {
                if (value == null || value is DBNull || !props.TryGetValue(column, out var prop))
                    continue;
                prop.SetValue(item, ConvertValue(value, prop.PropertyType, column));
            }
            return item;
        }

        private static IReadOnlyDictionary<string, PropertyInfo> WritableProperties(Type type)
            => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.GetIndexParameters().Length == 0)
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                // A derived class may hide a base property ("new"); prefer the most derived declaration.
                .ToDictionary(g => g.Key, g => g.OrderByDescending(p => Depth(p.DeclaringType)).First(), StringComparer.OrdinalIgnoreCase);

        private static int Depth(Type? type)
        {
            var depth = 0;
            while (type != null) { depth++; type = type.BaseType; }
            return depth;
        }

        /// <summary>Converts a SQLite value (long, double, string, byte[]) to <paramref name="targetType"/>.</summary>
        public static object? ConvertValue(object value, Type targetType, string column = "")
        {
            var type = Nullable.GetUnderlyingType(targetType) ?? targetType;
            try
            {
                if (type.IsInstanceOfType(value))
                    return value;

                if (type == typeof(string))
                    return Convert.ToString(value, CultureInfo.InvariantCulture);

                // Blank text in a non-text column (e.g. a date saved as '') means "no value".
                if (value is string blank && string.IsNullOrWhiteSpace(blank))
                    return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null ? Activator.CreateInstance(targetType) : null;

                if (type == typeof(string[]))
                    return Convert.ToString(value, CultureInfo.InvariantCulture)!.Split(',');

                if (type == typeof(Guid))
                    return value is byte[] bytes ? new Guid(bytes) : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!);

                if (type == typeof(bool))
                {
                    if (value is string s)
                        return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
                    return Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0;
                }

                if (type == typeof(DateTime))
                    return value is string ds
                        ? DateTime.Parse(ds, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces)
                        : Convert.ToDateTime(value, CultureInfo.InvariantCulture);

                if (type == typeof(DateTimeOffset))
                    return value is string dos
                        ? DateTimeOffset.Parse(dos, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal)
                        : new DateTimeOffset(Convert.ToDateTime(value, CultureInfo.InvariantCulture));

                if (type.IsEnum)
                    return value is string es
                        ? Enum.Parse(type, es, ignoreCase: true)
                        : Enum.ToObject(type, Convert.ToInt64(value, CultureInfo.InvariantCulture));

                return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
            {
                throw new InvalidCastException($"Cannot convert column '{column}' value '{value}' ({value.GetType().Name}) to {targetType.Name}.", ex);
            }
        }
    }
}
