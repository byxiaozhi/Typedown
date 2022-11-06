using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using Typedown.Universal.Models;

namespace Typedown.Universal.Services
{
    public class AppDbContext : DbContext
    {
        public DbSet<FileAccessHistory> FileAccessHistories { get; set; }

        public DbSet<FolderAccessHistory> FolderAccessHistories { get; set; }

        private readonly string dbPath = Path.Combine(Config.GetLocalFolderPath(), "Storage.db");

        private static readonly object lockMigrateTask = new();

        private static Task migrateTask;

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var builder = new SqliteConnectionStringBuilder() { DataSource = dbPath };
            options.UseSqlite(builder.ConnectionString);
        }

        public async Task EnsureMigrateAsync()
        {
            lock (lockMigrateTask)
                migrateTask ??= Database.MigrateAsync();
            await migrateTask;
        }
    }

    public static class AppDbContextExtensions
    {
        public static Task InitAppDbContext(this IServiceProvider serviceProvider)
        {
            return Task.Run(() => serviceProvider.GetService<AppDbContext>().EnsureMigrateAsync());
        }

        public static Task<AppDbContext> GetAppDbContext(this IServiceProvider serviceProvider)
        {
            return Task.Run(async () =>
            {
                var ctx = serviceProvider.GetService<AppDbContext>();
                await ctx.EnsureMigrateAsync();
                return ctx;
            });
        }
    }
}
