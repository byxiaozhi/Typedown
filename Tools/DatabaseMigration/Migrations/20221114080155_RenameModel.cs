using Microsoft.EntityFrameworkCore.Migrations;

namespace DatabaseMigration.Migrations
{
    public partial class RenameModel : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportSetting");

            migrationBuilder.DropTable(
                name: "ImageUploadSetting");

            migrationBuilder.CreateTable(
                name: "ExportConfig",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(nullable: true),
                    Notes = table.Column<string>(nullable: true),
                    Type = table.Column<int>(nullable: false),
                    Config = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportConfig", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImageUploadConfig",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(nullable: true),
                    Notes = table.Column<string>(nullable: true),
                    IsEnable = table.Column<bool>(nullable: false),
                    Method = table.Column<int>(nullable: false),
                    Config = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageUploadConfig", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExportConfig");

            migrationBuilder.DropTable(
                name: "ImageUploadConfig");

            migrationBuilder.CreateTable(
                name: "ExportSetting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Config = table.Column<string>(type: "TEXT", nullable: true),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExportSetting", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImageUploadSetting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Config = table.Column<string>(type: "TEXT", nullable: true),
                    IsEnable = table.Column<bool>(type: "INTEGER", nullable: false),
                    Method = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImageUploadSetting", x => x.Id);
                });
        }
    }
}
