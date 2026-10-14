using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Typedown.Core.Services;

namespace Typedown.ReliabilityTests
{
    [TestClass]
    public class DatabaseMigrationTests
    {
        private static readonly string[] ExpectedTables =
        {
            "ExportConfig", "FileAccessHistory", "FolderAccessHistory", "ImageUploadConfig", "__EFMigrationsHistory"
        };

        [TestMethod]
        public async Task CurrentMigrationsCreateTheExpectedSchemaFromEmpty()
        {
            using var directory = new TestDirectory();
            var path = directory.File("Storage.db");

            using (var context = new AppDbContext(path))
                await context.Database.MigrateAsync();

            CollectionAssert.AreEquivalent(ExpectedTables, await ReadNames(path, "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"));
            CollectionAssert.AreEquivalent(new[] { "Id", "Name", "Notes", "Type", "Config" }, await ReadPragmaNames(path, "ExportConfig"));
            CollectionAssert.AreEquivalent(new[] { "Id", "AccessTime", "FilePath" }, await ReadPragmaNames(path, "FileAccessHistory"));
            CollectionAssert.AreEquivalent(new[] { "Id", "AccessTime", "FolderPath" }, await ReadPragmaNames(path, "FolderAccessHistory"));
            CollectionAssert.AreEquivalent(new[] { "Id", "Name", "Notes", "IsEnable", "Method", "Config" }, await ReadPragmaNames(path, "ImageUploadConfig"));
            Assert.AreEqual(1L, await ScalarLong(path, "SELECT COUNT(*) FROM __EFMigrationsHistory"));
            Assert.AreEqual("ok", await ScalarString(path, "PRAGMA integrity_check"));
        }

        [TestMethod]
        public async Task LegacyDatabaseUpgradesWithoutLosingDataAndMigrationIsIdempotent()
        {
            using var directory = new TestDirectory();
            var path = directory.File("Storage.db");
            await CreateFromFixture(path, "legacy-schema.sql");

            using (var context = new AppDbContext(path))
                await context.Database.MigrateAsync();
            using (var context = new AppDbContext(path))
            {
                await context.Database.MigrateAsync();
                Assert.AreEqual(0, (await context.Database.GetPendingMigrationsAsync()).Count());
            }

            CollectionAssert.AreEquivalent(ExpectedTables, await ReadNames(path, "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"));
            Assert.AreEqual(@"C:\docs\legacy.md", await ScalarString(path, "SELECT FilePath FROM FileAccessHistory WHERE Id=11"));
            Assert.AreEqual(@"C:\docs", await ScalarString(path, "SELECT FolderPath FROM FolderAccessHistory WHERE Id=12"));
            Assert.AreEqual("Legacy PDF", await ScalarString(path, "SELECT Name FROM ExportConfig WHERE Id=13"));
            Assert.AreEqual("Legacy upload", await ScalarString(path, "SELECT Name FROM ImageUploadConfig WHERE Id=14"));
            Assert.AreEqual(1L, await ScalarLong(path, "SELECT COUNT(*) FROM __EFMigrationsHistory"));
            Assert.AreEqual("ok", await ScalarString(path, "PRAGMA integrity_check"));

            // INTEGER PRIMARY KEY is SQLite's durable row-identity index. Future schema changes must keep it.
            foreach (var table in ExpectedTables.Where(name => !name.StartsWith("__", StringComparison.Ordinal)))
                Assert.AreEqual("Id", await PrimaryKeyColumn(path, table), $"{table} lost its primary-key index");
        }

        private static async Task CreateFromFixture(string path, string fixture)
        {
            using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));
            await command.ExecuteNonQueryAsync();
        }

        private static async Task<string[]> ReadNames(string path, string sql)
        {
            var names = new List<string>();
            using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) names.Add(reader.GetString(0));
            return names.ToArray();
        }

        private static async Task<string[]> ReadPragmaNames(string path, string table)
        {
            var names = new List<string>();
            using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info('{table.Replace("'", "''")}')";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) names.Add(reader.GetString(1));
            return names.ToArray();
        }

        private static async Task<long> ScalarLong(string path, string sql) => Convert.ToInt64(await Scalar(path, sql));
        private static async Task<string> ScalarString(string path, string sql) => Convert.ToString(await Scalar(path, sql));

        private static async Task<object> Scalar(string path, string sql)
        {
            using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            return await command.ExecuteScalarAsync();
        }

        private static async Task<string> PrimaryKeyColumn(string path, string table)
        {
            using var connection = new SqliteConnection($"Data Source={path}");
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info('{table.Replace("'", "''")}')";
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                if (reader.GetInt32(5) > 0) return reader.GetString(1);
            return null;
        }
    }
}
