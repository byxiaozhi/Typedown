using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Typedown.Core.Models
{
    [Table("ExportConfig")]
    public class ExportConfig
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id { get; set; }
        public string Name { get; set; }
        public string Notes { get; set; }
        public int Type { get; set; }
        public string Config { get; set; }
    }

    [Table("FileAccessHistory")]
    public class FileAccessHistory
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id { get; set; }
        public DateTime AccessTime { get; set; }
        public string FilePath { get; set; }
    }

    [Table("FolderAccessHistory")]
    public class FolderAccessHistory
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id { get; set; }
        public DateTime AccessTime { get; set; }
        public string FolderPath { get; set; }
    }

    [Table("ImageUploadConfig")]
    public class ImageUploadConfig
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)] public int Id { get; set; }
        public string Name { get; set; }
        public string Notes { get; set; }
        public bool IsEnable { get; set; }
        public int Method { get; set; }
        public string Config { get; set; }
    }
}

namespace Typedown.Core.Services
{
    /// <summary>A test host for the production migration classes linked into this assembly.</summary>
    public class AppDbContext : DbContext
    {
        private readonly string databasePath;

        public AppDbContext(string databasePath) => this.databasePath = databasePath;

        public DbSet<Typedown.Core.Models.ExportConfig> ExportConfigs { get; set; }
        public DbSet<Typedown.Core.Models.FileAccessHistory> FileAccessHistories { get; set; }
        public DbSet<Typedown.Core.Models.FolderAccessHistory> FolderAccessHistories { get; set; }
        public DbSet<Typedown.Core.Models.ImageUploadConfig> ImageUploadConfigs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source={databasePath}", sqlite => sqlite.MigrationsAssembly(GetType().Assembly.FullName));
        }
    }
}
