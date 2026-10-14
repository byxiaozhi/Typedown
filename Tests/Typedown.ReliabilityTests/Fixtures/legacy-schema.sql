CREATE TABLE "__EFMigrationsHistory" (
    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
    "ProductVersion" TEXT NOT NULL
);

CREATE TABLE "ExportConfig" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ExportConfig" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NULL,
    "Notes" TEXT NULL,
    "Type" INTEGER NOT NULL,
    "Config" TEXT NULL
);

CREATE TABLE "FileAccessHistory" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_FileAccessHistory" PRIMARY KEY AUTOINCREMENT,
    "AccessTime" TEXT NOT NULL,
    "FilePath" TEXT NULL
);

CREATE TABLE "FolderAccessHistory" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_FolderAccessHistory" PRIMARY KEY AUTOINCREMENT,
    "AccessTime" TEXT NOT NULL,
    "FolderPath" TEXT NULL
);

CREATE TABLE "ImageUploadConfig" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ImageUploadConfig" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NULL,
    "Notes" TEXT NULL,
    "IsEnable" INTEGER NOT NULL,
    "Method" INTEGER NOT NULL,
    "Config" TEXT NULL
);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20230226122314_InitialCreate', '3.1.30');
INSERT INTO "FileAccessHistory" ("Id", "AccessTime", "FilePath")
VALUES (11, '2024-01-02 03:04:05', 'C:\docs\legacy.md');
INSERT INTO "FolderAccessHistory" ("Id", "AccessTime", "FolderPath")
VALUES (12, '2024-01-02 03:04:05', 'C:\docs');
INSERT INTO "ExportConfig" ("Id", "Name", "Notes", "Type", "Config")
VALUES (13, 'Legacy PDF', 'keep', 0, '{"PDF":{"PageSize":"A4"}}');
INSERT INTO "ImageUploadConfig" ("Id", "Name", "Notes", "IsEnable", "Method", "Config")
VALUES (14, 'Legacy upload', 'keep', 1, 0, '{"FTP":{"Host":"example.invalid"}}');
