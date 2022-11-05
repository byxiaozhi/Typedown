using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Typedown.Universal.Models;
using Windows.Storage;

namespace Typedown.Universal.Services
{
    public class Database : DbContext
    {
        public DbSet<FileAccessHistory> FileAccessHistories { get; set; }

        private readonly string dbPath = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Storage.db");

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var builder = new SqliteConnectionStringBuilder()
            {
                DataSource = dbPath,
            };
            options.UseSqlite(builder.ConnectionString);
        }
    }
}
