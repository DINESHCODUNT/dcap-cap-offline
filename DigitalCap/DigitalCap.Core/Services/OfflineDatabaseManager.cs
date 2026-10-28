using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace DigitalCap.Core.Services
{
    public class OfflineDatabaseManager
    {
        private readonly OneDrivePathService _pathService;
        private readonly string _templateDbPath;
        private readonly string _encryptionKey;

        public OfflineDatabaseManager(string webRootPath)
        {
            _pathService = new OneDrivePathService();
            _templateDbPath = Path.Combine(webRootPath, AppKeyService.EncryptedDb);
            _encryptionKey = AppKeyService.GetDecryptedKey();
        }

        public string EnsureWorkingDatabaseExists()
        {
            var workingDbPath = _pathService.GetDatabasePath();

            if (!File.Exists(workingDbPath))
            {
                if (File.Exists(_templateDbPath))
                {
                    var dir = Path.GetDirectoryName(workingDbPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    File.Copy(_templateDbPath, workingDbPath, overwrite: true);
                }
                else
                {
                    return _templateDbPath;
                }
            }

            return workingDbPath;
        }

        public string GetEffectiveConnectionString()
        {
            var dbPath = EnsureWorkingDatabaseExists();
            if (File.Exists(dbPath))
            {
                return $"Data Source={dbPath};Password={_encryptionKey};";
            }
            if (File.Exists(_templateDbPath))
            {
                return $"Data Source={_templateDbPath};Password={_encryptionKey};";
            }
            return $"Data Source={dbPath};Password={_encryptionKey};";
        }

        /// <summary>
        /// Brings the working database up to the schema the offline queries expect.
        /// Ported from digital-cap-offline's Startup.ConfigureServices, which applies the same
        /// tables, columns and seed rows on every launch. Each step is guarded so it is safe to
        /// re-run, and a failing step is reported without stopping the others.
        /// </summary>
        public void EnsureSchema(Action<string>? log = null)
        {
            var dbPath = EnsureWorkingDatabaseExists();
            if (!File.Exists(dbPath))
            {
                log?.Invoke($"Database not found at {dbPath}; schema not applied.");
                return;
            }

            using var cnn = new SqliteConnection($"Data Source={dbPath};Password={_encryptionKey};");
            cnn.Open();

            foreach (var (table, sql) in RequiredTables)
            {
                if (!TableExists(cnn, table))
                    Apply(cnn, log, $"create table {table}", sql);
            }

            foreach (var (table, column, sql) in RequiredColumns)
            {
                if (TableExists(cnn, table) && !ColumnExists(cnn, table, column))
                    Apply(cnn, log, $"add column {table}.{column}", sql);
            }

            ApplySeedData(cnn, log);
        }

        private static void ApplySeedData(SqliteConnection cnn, Action<string>? log)
        {
            if (Count(cnn, "VesselType") == 13)
                Apply(cnn, log, "seed VesselType", "INSERT INTO VesselType VALUES (14, 'Passenger Vessel');");

            if (Count(cnn, "TankType") == 7)
            {
                Apply(cnn, log, "seed TankType", @"
                    INSERT INTO TankType VALUES (8, 'Cargo Containment System Moss Type', 0, 10);
                    INSERT INTO TankType VALUES (9, 'Membrane Type Cargo Containment Gas Transport Type', 0, 10);
                    INSERT INTO TankType VALUES (10, 'Cargo Containment System SPB Type', 0, 10);
                    INSERT INTO TankType VALUES (11, 'Hold/Void Space', 0, 10);");
            }

            // The reference relies on duplicate-key errors to skip this; seeding only an empty
            // table gives the same result without depending on a key constraint existing.
            if (Count(cnn, "H_I_Grading") == 0)
            {
                Apply(cnn, log, "seed H_I_Grading", @"
                    INSERT INTO H_I_Grading (Id, LabelName, GradingConditionGroupId, SpecialGasGrading) VALUES
                    (1,'Overall Cleanliness',1,0),(2,'Structural Condition on Deck',1,0),
                    (3,'Coating Condition on Deck',1,0),(4,'Structural Condition of Tanks',1,0),
                    (5,'Coating Condition of Tanks',1,0),(6,'Pumproom',1,0),
                    (7,'Machinery Spaces',1,0),(8,'Accommodations',1,0),
                    (9,'Navigation/Communication/Safety Items',1,0),(10,'External Hull',1,0),
                    (11,'Overall Cleanliness',1,1),(12,'Structural Condition on Deck',1,1),
                    (13,'Coating Condition on Deck',1,1),(14,'Structural Condition of Tanks',1,1),
                    (15,'Coating Condition of Tanks',1,1),(16,'Structural Condition of Cargo Holds',1,1),
                    (17,'Coating Condition of Cargo Holds',1,1),(18,'Compressor/Motor room',1,1),
                    (19,'LNG/LPG Containment/Insulation',1,1),(20,'LNG/LPG Piping & Fittings',1,1),
                    (21,'LNG/LPG Equipment',1,1),(22,'Machinery Spaces',1,1),
                    (23,'Accommodation',1,1),(24,'Navigation/Communication/Safety Items',1,1),
                    (25,'External Hull',1,1);");
            }

            var mappingCount = Count(cnn, "Tank_ImageDescriptionMapping");
            if (mappingCount == 7)
            {
                Apply(cnn, log, "seed Tank_ImageDescriptionMapping", @"
                    INSERT INTO Tank_ImageDescriptionMapping VALUES (8, 47);
                    INSERT INTO Tank_ImageDescriptionMapping VALUES (9, 48);
                    INSERT INTO Tank_ImageDescriptionMapping VALUES (10, 49);
                    INSERT INTO Tank_ImageDescriptionMapping VALUES (11, 50);");
            }
            if (mappingCount > 7)
            {
                Apply(cnn, log, "update Tank_ImageDescriptionMapping", @"
                    UPDATE Tank_ImageDescriptionMapping SET CategoryId=32 WHERE TankTypeId=8;
                    UPDATE Tank_ImageDescriptionMapping SET CategoryId=33 WHERE TankTypeId=9;
                    UPDATE Tank_ImageDescriptionMapping SET CategoryId=34 WHERE TankTypeId=10;
                    UPDATE Tank_ImageDescriptionMapping SET CategoryId=35 WHERE TankTypeId=11;");
            }
        }

        private static void Apply(SqliteConnection cnn, Action<string>? log, string step, string sql)
        {
            try
            {
                using var cmd = cnn.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
                log?.Invoke($"Applied: {step}");
            }
            catch (Exception ex)
            {
                log?.Invoke($"Failed: {step}: {ex.Message}");
            }
        }

        private static bool TableExists(SqliteConnection cnn, string table)
        {
            using var cmd = cnn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name;";
            cmd.Parameters.AddWithValue("@name", table);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static bool ColumnExists(SqliteConnection cnn, string table, string column)
        {
            using var cmd = cnn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM pragma_table_info(@table) WHERE name = @column COLLATE NOCASE;";
            cmd.Parameters.AddWithValue("@table", table);
            cmd.Parameters.AddWithValue("@column", column);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        private static long Count(SqliteConnection cnn, string table)
        {
            if (!TableExists(cnn, table))
                return -1;

            using var cmd = cnn.CreateCommand();
            // Table names come only from the constants in this class.
            cmd.CommandText = $"SELECT COUNT(*) FROM [{table}];";
            return Convert.ToInt64(cmd.ExecuteScalar());
        }

        private static readonly (string Table, string Sql)[] RequiredTables =
        {
            // Desktop additions (no reference equivalent table; the reference kept this data in localStorage):
            // the downloading user's roles, permission claims and the role-page permission matrix, written by
            // OfflineTransferRepository.SaveDownloadedProjectAsync and read by UserPermissionRepository offline.
            ("OfflineUserSecurity", @"CREATE TABLE OfflineUserSecurity (
                UserName TEXT NOT NULL COLLATE NOCASE PRIMARY KEY,
                AspNetUserId TEXT NULL,
                StoredAt DATETIME NULL);"),
            ("OfflineUserRoles", @"CREATE TABLE OfflineUserRoles (
                UserName TEXT NOT NULL COLLATE NOCASE,
                RoleName TEXT NOT NULL COLLATE NOCASE);"),
            ("OfflineUserClaims", @"CREATE TABLE OfflineUserClaims (
                UserName TEXT NOT NULL COLLATE NOCASE,
                ClaimType TEXT NOT NULL);"),
            ("OfflineRolePermissions", @"CREATE TABLE OfflineRolePermissions (
                RoleName TEXT NOT NULL COLLATE NOCASE,
                PermissionName TEXT NOT NULL COLLATE NOCASE,
                [Read] BIT NOT NULL DEFAULT 0,
                [Edit] BIT NOT NULL DEFAULT 0,
                [Delete] BIT NOT NULL DEFAULT 0);"),
            ("AspNetUserLogins", @"CREATE TABLE AspNetUserLogins (
                LoginProvider TEXT NOT NULL,
                ProviderKey TEXT NOT NULL,
                ProviderDisplayName TEXT NULL,
                UserId TEXT NOT NULL,
                PRIMARY KEY (LoginProvider, ProviderKey));"),

            ("H_I_Grading", @"CREATE TABLE [H_I_Grading] (
                        [Id] INT IDENTITY (1, 1) NOT NULL,
                        [LabelName] NVARCHAR (50) NULL,
                        [GradingConditionGroupId] INT NULL,
                        [SpecialGasGrading] BIT NULL)"),

            ("ReportPart", @"CREATE TABLE [ReportPart] (
                        [PartId] [int] IDENTITY(1, 1) NOT NULL,
                        [ProjectId] [int] NULL,
                        [PartName] [nvarchar](100) NULL,
                        [SequenceNo] [int] NULL,
                        [IsActive] [bit] NULL,
                        [CreatedBy] [varchar](100) NULL,
                        [CreatedOn] [date] NULL,
                        [IsDeleted] [bit] NULL,
                        [ModifiedBy] [nvarchar](50) NULL,
                        [ModifiedOn] [date] NULL,
                        [RequiredInReport] [bit] NULL)"),

            ("ProjectTankSectionMapping", @"CREATE TABLE ProjectTankSectionMapping(
                        [TankSectionMappingId] [uniqueidentifier] NOT NULL,
                        [ProjectId] [int] NULL,
                        [PartId] [int] NULL,
                        [TankTypeId] [int] NULL,
                        [TotalCards] [int] NULL,
                        [PlaceholderCount] [int] NULL,
                        [FileNameCount] [int] NULL,
                        [IsActive] [bit] NULL,
                        [CreatedBy] [varchar](100) NULL,
                        [CreatedOn] [date] NULL,
                        [IsDeleted] [bit] NULL,
                        [ModifiedBy] [nvarchar](50) NULL,
                        [ModifiedOn] [date] NULL)"),

            ("ProjectSubSection", @"CREATE TABLE ProjectSubSection(
                        [Id] [uniqueidentifier] NOT NULL,
                        [ProjectSectionId] [uniqueidentifier] NOT NULL,
                        [SectionName] [nvarchar](50) NOT NULL,
                        [Totalcards] [int] NULL,
                        [FileNameCount] [int] NULL,
                        [PlaceholderCount] [int] NULL,
                        [IsActive] [bit] NULL,
                        [CreatedBy] [varchar](100) NULL,
                        [CreatedOn] [date] NULL,
                        [IsDeleted] [bit] NULL,
                        [ModifiedOn] [date] NULL,
                        [ModifiedBy] [nvarchar](50) NULL)"),

            ("ProjectSectionMapping", @"CREATE TABLE ProjectSectionMapping(
                        [SectionMappingId] [uniqueidentifier] NOT NULL,
                        [ProjectId] [int] NULL,
                        [PartId] [int] NULL,
                        [SectionName] [nvarchar](100) NULL,
                        [SubHeader] [nvarchar](100) NULL,
                        [TotalCards] [int] NULL,
                        [PlaceholderCount] [int] NULL,
                        [FileNameCount] [int] NULL,
                        [IsActive] [bit] NULL,
                        [CreatedBy] [varchar](100) NULL,
                        [CreatedOn] [date] NULL,
                        [IsDeleted] [bit] NULL,
                        [HasSubSection] [bit] NULL,
                        [Order] [int] NULL,
                        [ModifiedBy] [nvarchar](50) NULL,
                        [ModifiedOn] [date] NULL,
                        [RequiredInReport] [bit] NULL)"),

            ("ProjectSectionGradingMapping", @"CREATE TABLE ProjectSectionGradingMapping(
                        [SectionId] [uniqueidentifier] NOT NULL,
                        [CategoryId] [int] NOT NULL,
                        [IsActive] [bit] NULL,
                        [CreatedBy] [varchar](100) NULL,
                        [CreatedOn] [date] NULL,
                        [IsDeleted] [bit] NULL,
                        [ModifiedOn] [date] NULL,
                        [ModifiedBy] [nvarchar](50) NULL)"),

            ("ProjectSectionDescriptionMapping", @"CREATE TABLE [ProjectSectionDescriptionMapping] (
                        [SectionId] UNIQUEIDENTIFIER NOT NULL,
                        [CategoryId] INT NOT NULL,
                        [IsActive] BIT DEFAULT ((1)) NULL,
                        [CreatedBy] VARCHAR (100) NULL,
                        [CreatedOn] DATE DEFAULT (getdate()) NULL,
                        [IsDeleted] BIT NULL,
                        [ModifiedOn] DATE NULL,
                        [ModifiedBy] NVARCHAR (50) NULL,
                        IsSync BOOLEAN Null)"),

            ("ProjectSubSectionDescriptionMapping", @"CREATE TABLE [ProjectSubSectionDescriptionMapping] (
                        [SubSectionId] UNIQUEIDENTIFIER NOT NULL,
                        [CategoryId] INT NOT NULL,
                        [IsActive] BIT DEFAULT ((1)) NULL,
                        [CreatedBy] VARCHAR (100) NULL,
                        [CreatedOn] DATE DEFAULT (getdate()) NULL,
                        [IsDeleted] BIT NULL,
                        [ModifiedOn] DATE NULL,
                        [ModifiedBy] NVARCHAR (50) NULL,
                        IsSync BOOLEAN Null)"),

            ("GenericImageCard", @"CREATE TABLE GenericImageCard (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProjectId INTEGER,
                        TemplateId INTEGER,
                        SectionId GUID,
                        DescriptionId INTEGER,
                        AdditionalDescription NVARCHAR,
                        CurrentCondition INTEGER,
                        ImageId INTEGER,
                        CreatedDttm DATETIME,
                        UpdatedDttm DATETIME,
                        IsDeleted BIT,
                        IsSync BOOLEAN Null,
                        CardName text,
                        CardNumber number,
                        IsActive boolean)"),

            ("ReportPart_backup", @"CREATE TABLE ReportPart_backup(
                        [PartId] [int] IDENTITY(1,1) NOT NULL,
                        [ProjectId] [int] NULL,
                        [PartName] [nvarchar](100) NULL,
                        [SequenceNo] [int] NULL,
                        [IsActive] [bit] NULL default 1,
                        [CreatedBy] [varchar](100) NULL,
                        CreatedOn DATETIME DEFAULT CURRENT_TIMESTAMP,
                        [IsDeleted] [bit] NULL,
                        [ModifiedBy] [nvarchar](50) NULL,
                        [ModifiedOn] [datetime] NULL,
                        IsSync bit)"),

            ("ProjectSectionMapping_backup", @"CREATE TABLE ProjectSectionMapping_backup (
                        [SectionMappingId] [uniqueidentifier] NOT NULL,
                        [ProjectId] [int] NULL,
                        [PartId] [int] NULL,
                        [SectionName] [nvarchar](100) NULL,
                        [SubHeader] [nvarchar](100) NULL,
                        [TotalCards] [int] NULL,
                        [PlaceholderCount] [int] NULL,
                        [FileNameCount] [int] NULL,
                        [IsActive] [bit] NULL,
                        [CreatedBy] [varchar](100) NULL,
                        [CreatedOn] [date] NULL default CURRENT_TIMESTAMP,
                        [IsDeleted] [bit] NULL,
                        [HasSubSection] [bit] NULL,
                        [Order] [int] NULL,
                        [ModifiedBy] [nvarchar](50) NULL,
                        [ModifiedOn] [date] NULL,
                        [IsSync] [bit] null,
                        RequiredInReport bit NULL)"),

            ("Grading_backup", @"CREATE TABLE Grading_backup (
                        Id INTEGER NOT NULL,
                        LabelName NVARCHAR,
                        GradingGroupId INTEGER,
                        GradingConditionGroupId INTEGER,
                        IsActive BIT,
                        IsDeleted BIT,
                        VesselType NVARCHAR,
                        ProjectId INTEGER,
                        IsSync BOOLEAN,
                        RequiredInReport bit NULL)"),

            ("Vessel_GradingMapping_backup", @"CREATE TABLE Vessel_GradingMapping_backup (
                        Id int,
                        VesselType nvarchar,
                        TankTypeId int,
                        GradingName nvarchar,
                        GradingGroupId int,
                        GradingConditionGroupId int,
                        IsActive bit,
                        CreatedDttm datetime(8),
                        UpdateDttm datetime(8),
                        IsDeleted bit,
                        ProjectId int,
                        IsSync BOOLEAN,
                        RequiredInReport bit NULL)"),

            ("Vessel_TankMapping_backup", @"CREATE TABLE Vessel_TankMapping_backup (
                        Id GUID,
                        VesselType NVARCHAR,
                        ImoNumber NVARCHAR,
                        TankTypeId INT,
                        TankName NVARCHAR,
                        IsActive BIT,
                        CreatedDttm DATETIME,
                        UpdateDttm DATETIME,
                        IsDeleted BIT,
                        IsSync BOOLEAN,
                        Subheader varchar(1000),
                        RequiredInReport bit NULL)"),

            ("ProjectTankSectionMapping_backup", @"CREATE TABLE ProjectTankSectionMapping_backup(
                        [TankSectionMappingId] [TEXT] NOT NULL,
                        [ProjectId] [int] NULL,
                        [PartId] [int] NULL,
                        [TankTypeId] [int] NULL,
                        [TotalCards] [int] NULL,
                        [PlaceholderCount] [int] NULL,
                        [FileNameCount] [int] NULL,
                        [IsActive] [bit] NULL,
                        [CreatedBy] [varchar](100) NULL,
                        [CreatedOn] [date] DEFAULT CURRENT_TIMESTAMP,
                        [IsDeleted] [bit] NULL,
                        [ModifiedBy] [nvarchar](50) NULL,
                        [ModifiedOn] [date] NULL,
                        IsSync bit Null,
                        RequiredInReport bit NULL)"),

            ("ProjectTemplateMapping_backup", @"CREATE TABLE ProjectTemplateMapping_backup (
                        Id GUID NOT NULL,
                        ProjectId INTEGER,
                        TemplateId INTEGER,
                        SectionId GUID,
                        Notes NVARCHAR,
                        CreatedDttm DATETIME,
                        UpdatedDttm DATETIME,
                        IsDeleted BIT,
                        IsSync BOOLEAN)"),

            ("H_I_Grading_backup", @"CREATE TABLE H_I_Grading_backup(
                        [Id] [int] IDENTITY(1, 1) NOT NULL,
                        [LabelName] [nvarchar](50) NULL,
                        [GradingConditionGroupId] [int] NULL,
                        [SpecialGasGrading] [bit] NULL)"),

            ("H_IReportTemplate_backup", @"CREATE TABLE H_IReportTemplate_backup(
                        Id GUID,
                        ProjectId int,
                        CompanyName nvarchar,
                        CertificateNumber nvarchar,
                        ExpiryDate datetime(8),
                        ThicknessMeasurementA nvarchar,
                        ThicknessMeasurementB nvarchar,
                        ThicknessMeasurementC nvarchar,
                        AdditionalSurveyorComments nvarchar,
                        MaintenanceExecuted nvarchar,
                        Accommodation nvarchar,
                        PhotoGraphs nvarchar,
                        OperationalTestLocation nvarchar,
                        OperationalTestStart datetime(8),
                        OperationalTestFinish datetime(8),
                        OptionalTestSatisFactory nvarchar,
                        AssociatedReportNumber nvarchar,
                        AssociatedReportDate nvarchar,
                        Remarks nvarchar,
                        LeadSurveyorName nvarchar,
                        LeadSurveyorNumber nvarchar,
                        SurveyorName nvarchar,
                        SurveyorNumber nvarchar,
                        LastVisitDate datetime(8),
                        IndividualA nvarchar,
                        IndividualB nvarchar,
                        IndividualC nvarchar,
                        IndividualD nvarchar,
                        IndividualE nvarchar,
                        IndividualF nvarchar,
                        SubstantialCorrosionRemaining nvarchar,
                        StatutoryDeficiencies nvarchar,
                        FoundSatisfactory nvarchar,
                        UnderRemarks nvarchar,
                        ScouringOrPitting nvarchar,
                        BottomPlattingScouringOrPitting nvarchar,
                        IsActive bit,
                        IsDeleted bit,
                        CreatedDttm datetime(8),
                        UpdatedDttm datetime(8),
                        IsSync BOOLEAN)"),

            ("HandIProjectGrading", @"CREATE TABLE HandIProjectGrading (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProjectID INTEGER,
                        GradingId INTEGER,
                        Value TEXT,
                        IsActive BOOLEAN,
                        IsDeleted BOOLEAN,
                        CreatedBY TEXT,
                        CreatedOn DATE,
                        ModifiedBY TEXT,
                        ModifiedOn DATE,
                        IsSync BOOLEAN)"),

            ("HandIProjectGradingConditions", @"CREATE TABLE HandIProjectGradingConditions(
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        GradingID INTEGER,
                        ProjectId INTEGER,
                        ConditionLabel TEXT,
                        Value BOOLEAN,
                        IsActive BOOLEAN,
                        IsDeleted BOOLEAN,
                        CreatedBY TEXT,
                        CreatedOn DATE,
                        ModifiedBY TEXT,
                        ModifiedOn DATE,
                        IsSync BOOLEAN)"),

            ("HandIProjectGrading_backup", @"CREATE TABLE HandIProjectGrading_backup(
                        Id INTEGER,
                        ProjectID INTEGER,
                        GradingId INTEGER,
                        Value TEXT,
                        IsActive BOOLEAN,
                        IsDeleted BOOLEAN,
                        CreatedBY TEXT,
                        CreatedOn DATE,
                        ModifiedBY TEXT,
                        ModifiedOn DATE,
                        IsSync BOOLEAN)"),

            ("HandIProjectGradingConditions_backup", @"CREATE TABLE HandIProjectGradingConditions_backup(
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        GradingID INTEGER,
                        ProjectId INTEGER,
                        ConditionLabel TEXT,
                        Value BOOLEAN,
                        IsActive BOOLEAN,
                        IsDeleted BOOLEAN,
                        CreatedBY TEXT,
                        CreatedOn DATE,
                        ModifiedBY TEXT,
                        ModifiedOn DATE,
                        IsSync BOOLEAN)"),

            ("ProjectSubSection_backup", @"CREATE TABLE ProjectSubSection_backup(
                        [Id] [uniqueidentifier] NOT NULL,
                        [ProjectSectionId] [uniqueidentifier] NOT NULL,
                        [SectionName] [nvarchar](50) NOT NULL,
                        [Totalcards] [int] NULL,
                        [FileNameCount] [int] NULL,
                        [PlaceholderCount] [int] NULL,
                        [IsActive] [bit] NULL,
                        [CreatedBy] [varchar](100) NULL,
                        [CreatedOn] [date] NULL default CURRENT_TIMESTAMP,
                        [IsDeleted] [bit] NULL,
                        [ModifiedOn] [date] NULL,
                        [ModifiedBy] [nvarchar](50) NULL,
                        [OrderNumber] [int] NULL,
                        [IsSync] [bit] null)"),

            ("ProjectSectionGradingMapping_backup", @"CREATE TABLE ProjectSectionGradingMapping_backup(
                        [SectionId] [uniqueidentifier] NOT NULL,
                        [CategoryId] [int] NOT NULL,
                        [IsActive] [bit] NULL,
                        [CreatedBy] [varchar](100) NULL,
                        [CreatedOn] [date] NULL default CURRENT_TIMESTAMP,
                        [IsDeleted] [bit] NULL,
                        [ModifiedOn] [date] NULL,
                        [ModifiedBy] [nvarchar](50) NULL,
                        [IsSync] [bit] null)"),

            ("ProjectSubSectionDescriptionMapping_backup", @"CREATE TABLE ProjectSubSectionDescriptionMapping_backup(
                        [SubSectionId] [uniqueidentifier] NOT NULL,
                        [CategoryId] [int] NOT NULL,
                        [IsActive] [bit] NULL,
                        [CreatedBy] [varchar](100) NULL,
                        [CreatedOn] [date] NULL default CURRENT_TIMESTAMP,
                        [IsDeleted] [bit] NULL,
                        [ModifiedOn] [date] NULL,
                        [ModifiedBy] [nvarchar](50) NULL,
                        [IsSync] [bit] null)"),

            ("ProjectSectionDescriptionMapping_backup", @"CREATE TABLE ProjectSectionDescriptionMapping_backup(
                        [SectionId] [uniqueidentifier] NOT NULL,
                        [CategoryId] [int] NOT NULL,
                        [IsActive] [bit] NULL,
                        [CreatedBy] [varchar](100) NULL,
                        [CreatedOn] [date] NULL default CURRENT_TIMESTAMP,
                        [IsDeleted] [bit] NULL,
                        [ModifiedOn] [date] NULL,
                        [ModifiedBy] [nvarchar](50) NULL,
                        [IsSync] [bit] null)"),

            ("GenericImageCard_backup", @"CREATE TABLE GenericImageCard_backup (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProjectId INTEGER,
                        TemplateId INTEGER,
                        SectionId GUID,
                        DescriptionId INTEGER,
                        AdditionalDescription NVARCHAR,
                        CurrentCondition INTEGER,
                        ImageId INTEGER,
                        CreatedDttm DATETIME,
                        UpdatedDttm DATETIME,
                        IsDeleted BIT,
                        IsSync BOOLEAN,
                        CardName text,
                        CardNumber number,
                        IsActive boolean)"),

            ("VesselTypeTankSectionMapping", @"CREATE TABLE VesselTypeTankSectionMapping(
                        [VesselTypeTankSectionMappingId] [uniqueidentifier] NOT NULL,
                        [VesselTypePartMappingId] [int] NULL,
                        [VesselTypeId] [int] NULL,
                        [TankTypeId] [int] NULL,
                        [TotalCards] [int] NULL,
                        [PlaceholderCount] [int] NULL,
                        [FileNameCount] [int] NULL,
                        [IsActive] [bit] NULL,
                        [CreatedBy] [varchar](100) NULL,
                        [CreatedOn] [date] NULL,
                        [IsDeleted] [bit] NULL,
                        [ModifiedBy] [nvarchar](50) NULL,
                        [ModifiedOn] [date] NULL)"),

        };

        private static readonly (string Table, string Column, string Sql)[] RequiredColumns =
        {
            ("Projects", "IsReadOnlyEnabled", "ALTER TABLE Projects ADD COLUMN [IsReadOnlyEnabled] BIT DEFAULT 0;"),
            ("Projects_backup", "IsReadOnlyEnabled", "ALTER TABLE Projects_backup ADD COLUMN [IsReadOnlyEnabled] BIT DEFAULT 0;"),
            ("Projects", "IsDeleted", "ALTER TABLE Projects ADD COLUMN [IsDeleted] BIT DEFAULT False;"),
            ("Projects_backup", "IsDeleted", "ALTER TABLE Projects_backup  ADD COLUMN [IsDeleted] BIT DEFAULT False;"),
            ("ProjectGrades", "IsDeleted", "ALTER TABLE ProjectGrades ADD COLUMN [IsDeleted] BIT DEFAULT False;"),
            ("ProjectTaskStatus", "IsDeleted", "ALTER TABLE ProjectTaskStatus ADD COLUMN [IsDeleted] BIT DEFAULT False;"),
            ("Comments", "IsDeleted", "ALTER TABLE Comments ADD COLUMN [IsDeleted] BIT DEFAULT False;"),
            ("Comments_backup", "IsDeleted", "ALTER TABLE Comments_backup ADD COLUMN [IsDeleted] BIT DEFAULT False;"),
            ("Grading", "RequiredInReport", "ALTER TABLE Grading ADD COLUMN [RequiredInReport] BIT DEFAULT True;"),
            ("ProjectGradingMapping", "RequiredInReport", "ALTER TABLE [ProjectGradingMapping] ADD COLUMN [RequiredInReport] BIT DEFAULT True;"),
            ("ReportStatutoryCertificates", "fitnessGasCertificateIssueDate", "ALTER TABLE [ReportStatutoryCertificates] ADD COLUMN [fitnessGasCertificateIssueDate] DATETIME DEFAULT NULL;"),
            ("ReportStatutoryCertificates", "fitnessGasCertificateExpiryDate", "ALTER TABLE [ReportStatutoryCertificates] ADD COLUMN [fitnessGasCertificateExpiryDate] DATETIME NULL;"),
            ("Vessel_GradingMapping", "RequiredInReport", "ALTER TABLE [Vessel_GradingMapping] ADD COLUMN [RequiredInReport] BIT DEFAULT True;"),
            ("ReportStatutoryCertificates_backup", "fitnessGasCertificateIssueDate", "ALTER TABLE [ReportStatutoryCertificates_backup] ADD COLUMN [fitnessGasCertificateIssueDate] DATETIME DEFAULT NULL;"),
            ("ReportStatutoryCertificates_backup", "fitnessGasCertificateExpiryDate", "ALTER TABLE [ReportStatutoryCertificates_backup] ADD COLUMN [fitnessGasCertificateExpiryDate] DATETIME NULL;"),
            ("Vessel_TankMapping_backup", "Subheader", "ALTER TABLE Vessel_TankMapping_backup ADD COLUMN Subheader TEXT NULL;"),
            ("Vessel_TankMapping_backup", "UpdatedBy", "ALTER TABLE Vessel_TankMapping_backup ADD COLUMN UpdatedBy TEXT NULL;"),
            ("ProjectSubSection_backup", "OrderNumber", "ALTER TABLE [ProjectSubSection_backup] ADD COLUMN [OrderNumber] Int Null;"),
            ("ProjectSubSection_backup", "RequiredInReport", "ALTER TABLE [ProjectSubSection_backup] ADD COLUMN [RequiredInReport] BIT NULL;"),
            ("Vessel_TankMapping", "Subheader", "ALTER TABLE Vessel_TankMapping ADD COLUMN Subheader TEXT NULL;"),
            ("Vessel_TankMapping", "UpdatedBy", "ALTER TABLE Vessel_TankMapping ADD COLUMN UpdatedBy TEXT NULL;"),
            ("Vessel_TankMapping", "RequiredInReport", "ALTER TABLE Vessel_TankMapping ADD COLUMN RequiredInReport BIT NULL;"),
            ("ReportPart", "RequiredInReport", "ALTER TABLE ReportPart ADD COLUMN RequiredInReport BIT NULL;"),
            ("ReportPart", "IsSync", "ALTER TABLE ReportPart ADD COLUMN IsSync BIT NULL;"),
            ("ProjectSubSection", "RequiredInReport", "ALTER TABLE ProjectSubSection ADD COLUMN RequiredInReport BIT NULL;"),
            ("GenericImageCard", "IsSync", "ALTER TABLE GenericImageCard ADD COLUMN IsSync BOOLEAN Null;"),
            ("ProjectSectionDescriptionMapping", "IsSync", "ALTER TABLE ProjectSectionDescriptionMapping ADD COLUMN IsSync BOOLEAN Null;"),
            ("ProjectTankSectionMapping", "IsSync", "ALTER TABLE ProjectTankSectionMapping ADD COLUMN IsSync BOOLEAN Null;"),
            ("ProjectSubSection", "IsSync", "ALTER TABLE ProjectSubSection ADD COLUMN IsSync BOOLEAN Null;"),
            ("ProjectSectionMapping", "IsSync", "ALTER TABLE ProjectSectionMapping ADD COLUMN IsSync BOOLEAN Null;"),
            ("ProjectSectionGradingMapping", "IsSync", "ALTER TABLE ProjectSectionGradingMapping ADD COLUMN IsSync BOOLEAN Null;"),
            ("ProjectSectionMapping", "RequiredInReport", "ALTER TABLE ProjectSectionMapping ADD COLUMN RequiredInReport BIT NULL;"),
            ("ProjectSubSection", "OrderNumber", "ALTER TABLE ProjectSubSection ADD COLUMN OrderNumber int null;"),
            ("ProjectTankSectionMapping", "RequiredInReport", "ALTER TABLE ProjectTankSectionMapping ADD COLUMN RequiredInReport BIT NULL;"),
            ("ReportPart_backup", "RequiredInReport", "ALTER TABLE ReportPart_backup ADD COLUMN RequiredInReport bit NULL;"),
            ("ProjectGradingMapping_backup", "RequiredInReport", "ALTER TABLE ProjectGradingMapping_backup ADD COLUMN RequiredInReport bit NULL;"),
            ("Grading_backup", "RequiredInReport", "ALTER TABLE Grading_backup ADD COLUMN RequiredInReport bit NULL;"),
            ("Vessel_GradingMapping_backup", "RequiredInReport", "ALTER TABLE Vessel_GradingMapping_backup ADD COLUMN RequiredInReport bit NULL;"),
            ("Vessel_TankMapping_backup", "RequiredInReport", "ALTER TABLE Vessel_TankMapping_backup ADD COLUMN RequiredInReport bit NULL;"),
            ("ProjectSectionMapping_backup", "RequiredInReport", "ALTER TABLE ProjectSectionMapping_backup ADD COLUMN RequiredInReport bit NULL;"),
            ("ProjectTankSectionMapping_backup", "RequiredInReport", "ALTER TABLE ProjectTankSectionMapping_backup ADD COLUMN RequiredInReport bit NULL;"),
            ("Vessel_TankMapping", "ProjectId", "ALTER TABLE Vessel_TankMapping ADD ProjectId int DEFAULT NULL;"),
            ("Vessel_TankMapping_backup", "ProjectId", "ALTER TABLE Vessel_TankMapping_backup ADD ProjectId int DEFAULT NULL;"),
            ("H_IReportTemplate", "OthersDescription", "ALTER TABLE H_IReportTemplate ADD COLUMN OthersDescription TEXT NULL;"),
            ("H_IReportTemplate_backup", "OthersDescription", "ALTER TABLE H_IReportTemplate_backup ADD COLUMN OthersDescription TEXT NULL;"),
        };
    }
}
