using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Quartz;
using Quartz.Impl.AdoJobStore;

namespace CanDoItAll.Modules.Automation;

internal static class AutomationQuartzPersistentStoreConfigurator
{
    private const string TablePrefix = "QRTZ_";

    public static void Configure(
        IServiceCollectionQuartzConfigurator quartz,
        IConfiguration configuration,
        string? contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(quartz);
        ArgumentNullException.ThrowIfNull(configuration);

        var databaseOptions = configuration.GetSection("Database").Get<DatabaseOptions>() ?? new DatabaseOptions();
        var provider = databaseOptions.Provider.Trim();
        if (provider.Equals("InMemory", StringComparison.OrdinalIgnoreCase) ||
            provider.Equals("Memory", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        quartz.UsePersistentStore(store =>
        {
            store.UseProperties = true;
            store.PerformSchemaValidation = true;
            store.RetryInterval = TimeSpan.FromSeconds(10);
            store.UseNewtonsoftJsonSerializer();

            if (provider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
            {
                var connectionString = ResolveSqliteConnectionString(databaseOptions.ConnectionString, contentRootPath);
                store.UseMicrosoftSQLite(ado =>
                {
                    ado.ConnectionString = SqliteWriteCoordination.NormalizeConnectionString(connectionString);
                    ado.TablePrefix = TablePrefix;
                });
                return;
            }

            if (provider.Equals("Postgres", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
            {
                store.UseGenericDatabase<PostgreSQLDelegate>("Npgsql", ado =>
                {
                    ado.ConnectionString = string.IsNullOrWhiteSpace(databaseOptions.ConnectionString)
                        ? "Host=localhost;Database=candoitall;Username=postgres;Password=postgres"
                        : databaseOptions.ConnectionString;
                    ado.TablePrefix = TablePrefix;
                });
                return;
            }

            throw new InvalidOperationException(
                $"Quartz persistent automation scheduling does not support database provider '{databaseOptions.Provider}'.");
        });
    }

    private static string ResolveSqliteConnectionString(string? configuredConnectionString, string? contentRootPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return configuredConnectionString;
        }

        if (string.IsNullOrWhiteSpace(contentRootPath))
        {
            throw new InvalidOperationException(
                "SQLite Quartz persistence requires a database connection string or a content root path.");
        }

        var databasePath = Path.Combine(contentRootPath, ".artifacts", "workspace", "candoitall.db");
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        return $"Data Source={databasePath}";
    }
}

public static class AutomationQuartzSchemaInitializer
{
    public static async Task EnsureAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        var providerName = dbContext.Database.ProviderName ?? string.Empty;
        if (providerName.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(SqliteSchema, cancellationToken);
            return;
        }

        if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(PostgreSqlSchema, cancellationToken);
        }
    }

    private const string SqliteSchema =
        """
        CREATE TABLE IF NOT EXISTS QRTZ_JOB_DETAILS (
            SCHED_NAME NVARCHAR(120) NOT NULL,
            JOB_NAME NVARCHAR(150) NOT NULL,
            JOB_GROUP NVARCHAR(150) NOT NULL,
            DESCRIPTION NVARCHAR(250) NULL,
            JOB_CLASS_NAME NVARCHAR(250) NOT NULL,
            IS_DURABLE BIT NOT NULL,
            IS_NONCONCURRENT BIT NOT NULL,
            IS_UPDATE_DATA BIT NOT NULL,
            REQUESTS_RECOVERY BIT NOT NULL,
            JOB_DATA BLOB NULL,
            PRIMARY KEY (SCHED_NAME, JOB_NAME, JOB_GROUP)
        );
        CREATE TABLE IF NOT EXISTS QRTZ_TRIGGERS (
            SCHED_NAME NVARCHAR(120) NOT NULL,
            TRIGGER_NAME NVARCHAR(150) NOT NULL,
            TRIGGER_GROUP NVARCHAR(150) NOT NULL,
            JOB_NAME NVARCHAR(150) NOT NULL,
            JOB_GROUP NVARCHAR(150) NOT NULL,
            DESCRIPTION NVARCHAR(250) NULL,
            NEXT_FIRE_TIME BIGINT NULL,
            PREV_FIRE_TIME BIGINT NULL,
            PRIORITY INTEGER NULL,
            TRIGGER_STATE NVARCHAR(16) NOT NULL,
            TRIGGER_TYPE NVARCHAR(8) NOT NULL,
            START_TIME BIGINT NOT NULL,
            END_TIME BIGINT NULL,
            CALENDAR_NAME NVARCHAR(200) NULL,
            MISFIRE_INSTR INTEGER NULL,
            JOB_DATA BLOB NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, JOB_NAME, JOB_GROUP)
                REFERENCES QRTZ_JOB_DETAILS(SCHED_NAME, JOB_NAME, JOB_GROUP)
        );
        CREATE TABLE IF NOT EXISTS QRTZ_SIMPLE_TRIGGERS (
            SCHED_NAME NVARCHAR(120) NOT NULL,
            TRIGGER_NAME NVARCHAR(150) NOT NULL,
            TRIGGER_GROUP NVARCHAR(150) NOT NULL,
            REPEAT_COUNT BIGINT NOT NULL,
            REPEAT_INTERVAL BIGINT NOT NULL,
            TIMES_TRIGGERED BIGINT NOT NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
                REFERENCES QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP) ON DELETE CASCADE
        );
        CREATE TRIGGER IF NOT EXISTS DELETE_SIMPLE_TRIGGER DELETE ON QRTZ_TRIGGERS
        BEGIN
            DELETE FROM QRTZ_SIMPLE_TRIGGERS WHERE SCHED_NAME = OLD.SCHED_NAME AND TRIGGER_NAME = OLD.TRIGGER_NAME AND TRIGGER_GROUP = OLD.TRIGGER_GROUP;
        END;
        CREATE TABLE IF NOT EXISTS QRTZ_SIMPROP_TRIGGERS (
            SCHED_NAME NVARCHAR(120) NOT NULL,
            TRIGGER_NAME NVARCHAR(150) NOT NULL,
            TRIGGER_GROUP NVARCHAR(150) NOT NULL,
            STR_PROP_1 NVARCHAR(512) NULL,
            STR_PROP_2 NVARCHAR(512) NULL,
            STR_PROP_3 NVARCHAR(512) NULL,
            INT_PROP_1 INT NULL,
            INT_PROP_2 INT NULL,
            LONG_PROP_1 BIGINT NULL,
            LONG_PROP_2 BIGINT NULL,
            DEC_PROP_1 NUMERIC NULL,
            DEC_PROP_2 NUMERIC NULL,
            BOOL_PROP_1 BIT NULL,
            BOOL_PROP_2 BIT NULL,
            TIME_ZONE_ID NVARCHAR(80) NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
                REFERENCES QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP) ON DELETE CASCADE
        );
        CREATE TRIGGER IF NOT EXISTS DELETE_SIMPROP_TRIGGER DELETE ON QRTZ_TRIGGERS
        BEGIN
            DELETE FROM QRTZ_SIMPROP_TRIGGERS WHERE SCHED_NAME = OLD.SCHED_NAME AND TRIGGER_NAME = OLD.TRIGGER_NAME AND TRIGGER_GROUP = OLD.TRIGGER_GROUP;
        END;
        CREATE TABLE IF NOT EXISTS QRTZ_CRON_TRIGGERS (
            SCHED_NAME NVARCHAR(120) NOT NULL,
            TRIGGER_NAME NVARCHAR(150) NOT NULL,
            TRIGGER_GROUP NVARCHAR(150) NOT NULL,
            CRON_EXPRESSION NVARCHAR(250) NOT NULL,
            TIME_ZONE_ID NVARCHAR(80),
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
                REFERENCES QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP) ON DELETE CASCADE
        );
        CREATE TRIGGER IF NOT EXISTS DELETE_CRON_TRIGGER DELETE ON QRTZ_TRIGGERS
        BEGIN
            DELETE FROM QRTZ_CRON_TRIGGERS WHERE SCHED_NAME = OLD.SCHED_NAME AND TRIGGER_NAME = OLD.TRIGGER_NAME AND TRIGGER_GROUP = OLD.TRIGGER_GROUP;
        END;
        CREATE TABLE IF NOT EXISTS QRTZ_BLOB_TRIGGERS (
            SCHED_NAME NVARCHAR(120) NOT NULL,
            TRIGGER_NAME NVARCHAR(150) NOT NULL,
            TRIGGER_GROUP NVARCHAR(150) NOT NULL,
            BLOB_DATA BLOB NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP),
            FOREIGN KEY (SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP)
                REFERENCES QRTZ_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP) ON DELETE CASCADE
        );
        CREATE TRIGGER IF NOT EXISTS DELETE_BLOB_TRIGGER DELETE ON QRTZ_TRIGGERS
        BEGIN
            DELETE FROM QRTZ_BLOB_TRIGGERS WHERE SCHED_NAME = OLD.SCHED_NAME AND TRIGGER_NAME = OLD.TRIGGER_NAME AND TRIGGER_GROUP = OLD.TRIGGER_GROUP;
        END;
        CREATE TABLE IF NOT EXISTS QRTZ_CALENDARS (
            SCHED_NAME NVARCHAR(120) NOT NULL,
            CALENDAR_NAME NVARCHAR(200) NOT NULL,
            CALENDAR BLOB NOT NULL,
            PRIMARY KEY (SCHED_NAME, CALENDAR_NAME)
        );
        CREATE TABLE IF NOT EXISTS QRTZ_PAUSED_TRIGGER_GRPS (
            SCHED_NAME NVARCHAR(120) NOT NULL,
            TRIGGER_GROUP NVARCHAR(150) NOT NULL,
            PRIMARY KEY (SCHED_NAME, TRIGGER_GROUP)
        );
        CREATE TABLE IF NOT EXISTS QRTZ_FIRED_TRIGGERS (
            SCHED_NAME NVARCHAR(120) NOT NULL,
            ENTRY_ID NVARCHAR(140) NOT NULL,
            TRIGGER_NAME NVARCHAR(150) NOT NULL,
            TRIGGER_GROUP NVARCHAR(150) NOT NULL,
            INSTANCE_NAME NVARCHAR(200) NOT NULL,
            FIRED_TIME BIGINT NOT NULL,
            SCHED_TIME BIGINT NOT NULL,
            PRIORITY INTEGER NOT NULL,
            STATE NVARCHAR(16) NOT NULL,
            JOB_NAME NVARCHAR(150) NULL,
            JOB_GROUP NVARCHAR(150) NULL,
            IS_NONCONCURRENT BIT NULL,
            REQUESTS_RECOVERY BIT NULL,
            PRIMARY KEY (SCHED_NAME, ENTRY_ID)
        );
        CREATE TABLE IF NOT EXISTS QRTZ_SCHEDULER_STATE (
            SCHED_NAME NVARCHAR(120) NOT NULL,
            INSTANCE_NAME NVARCHAR(200) NOT NULL,
            LAST_CHECKIN_TIME BIGINT NOT NULL,
            CHECKIN_INTERVAL BIGINT NOT NULL,
            PRIMARY KEY (SCHED_NAME, INSTANCE_NAME)
        );
        CREATE TABLE IF NOT EXISTS QRTZ_LOCKS (
            SCHED_NAME NVARCHAR(120) NOT NULL,
            LOCK_NAME NVARCHAR(40) NOT NULL,
            PRIMARY KEY (SCHED_NAME, LOCK_NAME)
        );
        CREATE INDEX IF NOT EXISTS IDX_QRTZ_J_REQ_RECOVERY ON QRTZ_JOB_DETAILS(REQUESTS_RECOVERY);
        CREATE INDEX IF NOT EXISTS IDX_QRTZ_T_NEXT_FIRE_TIME ON QRTZ_TRIGGERS(NEXT_FIRE_TIME);
        CREATE INDEX IF NOT EXISTS IDX_QRTZ_T_STATE ON QRTZ_TRIGGERS(TRIGGER_STATE);
        CREATE INDEX IF NOT EXISTS IDX_QRTZ_T_NFT_ST ON QRTZ_TRIGGERS(NEXT_FIRE_TIME, TRIGGER_STATE);
        CREATE INDEX IF NOT EXISTS IDX_QRTZ_FT_TRIG_NAME ON QRTZ_FIRED_TRIGGERS(TRIGGER_NAME);
        CREATE INDEX IF NOT EXISTS IDX_QRTZ_FT_TRIG_GROUP ON QRTZ_FIRED_TRIGGERS(TRIGGER_GROUP);
        CREATE INDEX IF NOT EXISTS IDX_QRTZ_FT_TRIG_NM_GP ON QRTZ_FIRED_TRIGGERS(SCHED_NAME, TRIGGER_NAME, TRIGGER_GROUP);
        CREATE INDEX IF NOT EXISTS IDX_QRTZ_FT_TRIG_INST_NAME ON QRTZ_FIRED_TRIGGERS(INSTANCE_NAME);
        CREATE INDEX IF NOT EXISTS IDX_QRTZ_FT_JOB_NAME ON QRTZ_FIRED_TRIGGERS(JOB_NAME);
        CREATE INDEX IF NOT EXISTS IDX_QRTZ_FT_JOB_GROUP ON QRTZ_FIRED_TRIGGERS(JOB_GROUP);
        CREATE INDEX IF NOT EXISTS IDX_QRTZ_FT_JOB_REQ_RECOVERY ON QRTZ_FIRED_TRIGGERS(REQUESTS_RECOVERY);
        """;

    private const string PostgreSqlSchema =
        """
        CREATE TABLE IF NOT EXISTS qrtz_job_details (
            sched_name TEXT NOT NULL,
            job_name TEXT NOT NULL,
            job_group TEXT NOT NULL,
            description TEXT NULL,
            job_class_name TEXT NOT NULL,
            is_durable BOOL NOT NULL,
            is_nonconcurrent BOOL NOT NULL,
            is_update_data BOOL NOT NULL,
            requests_recovery BOOL NOT NULL,
            job_data BYTEA NULL,
            PRIMARY KEY (sched_name, job_name, job_group)
        );
        CREATE TABLE IF NOT EXISTS qrtz_triggers (
            sched_name TEXT NOT NULL,
            trigger_name TEXT NOT NULL,
            trigger_group TEXT NOT NULL,
            job_name TEXT NOT NULL,
            job_group TEXT NOT NULL,
            description TEXT NULL,
            next_fire_time BIGINT NULL,
            prev_fire_time BIGINT NULL,
            priority INTEGER NULL,
            trigger_state TEXT NOT NULL,
            trigger_type TEXT NOT NULL,
            start_time BIGINT NOT NULL,
            end_time BIGINT NULL,
            calendar_name TEXT NULL,
            misfire_instr SMALLINT NULL,
            job_data BYTEA NULL,
            PRIMARY KEY (sched_name, trigger_name, trigger_group),
            FOREIGN KEY (sched_name, job_name, job_group)
                REFERENCES qrtz_job_details(sched_name, job_name, job_group)
        );
        CREATE TABLE IF NOT EXISTS qrtz_simple_triggers (
            sched_name TEXT NOT NULL,
            trigger_name TEXT NOT NULL,
            trigger_group TEXT NOT NULL,
            repeat_count BIGINT NOT NULL,
            repeat_interval BIGINT NOT NULL,
            times_triggered BIGINT NOT NULL,
            PRIMARY KEY (sched_name, trigger_name, trigger_group),
            FOREIGN KEY (sched_name, trigger_name, trigger_group)
                REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE
        );
        CREATE TABLE IF NOT EXISTS qrtz_simprop_triggers (
            sched_name TEXT NOT NULL,
            trigger_name TEXT NOT NULL,
            trigger_group TEXT NOT NULL,
            str_prop_1 TEXT NULL,
            str_prop_2 TEXT NULL,
            str_prop_3 TEXT NULL,
            int_prop_1 INTEGER NULL,
            int_prop_2 INTEGER NULL,
            long_prop_1 BIGINT NULL,
            long_prop_2 BIGINT NULL,
            dec_prop_1 NUMERIC NULL,
            dec_prop_2 NUMERIC NULL,
            bool_prop_1 BOOL NULL,
            bool_prop_2 BOOL NULL,
            time_zone_id TEXT NULL,
            PRIMARY KEY (sched_name, trigger_name, trigger_group),
            FOREIGN KEY (sched_name, trigger_name, trigger_group)
                REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE
        );
        CREATE TABLE IF NOT EXISTS qrtz_cron_triggers (
            sched_name TEXT NOT NULL,
            trigger_name TEXT NOT NULL,
            trigger_group TEXT NOT NULL,
            cron_expression TEXT NOT NULL,
            time_zone_id TEXT,
            PRIMARY KEY (sched_name, trigger_name, trigger_group),
            FOREIGN KEY (sched_name, trigger_name, trigger_group)
                REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE
        );
        CREATE TABLE IF NOT EXISTS qrtz_blob_triggers (
            sched_name TEXT NOT NULL,
            trigger_name TEXT NOT NULL,
            trigger_group TEXT NOT NULL,
            blob_data BYTEA NULL,
            PRIMARY KEY (sched_name, trigger_name, trigger_group),
            FOREIGN KEY (sched_name, trigger_name, trigger_group)
                REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE
        );
        CREATE TABLE IF NOT EXISTS qrtz_calendars (
            sched_name TEXT NOT NULL,
            calendar_name TEXT NOT NULL,
            calendar BYTEA NOT NULL,
            PRIMARY KEY (sched_name, calendar_name)
        );
        CREATE TABLE IF NOT EXISTS qrtz_paused_trigger_grps (
            sched_name TEXT NOT NULL,
            trigger_group TEXT NOT NULL,
            PRIMARY KEY (sched_name, trigger_group)
        );
        CREATE TABLE IF NOT EXISTS qrtz_fired_triggers (
            sched_name TEXT NOT NULL,
            entry_id TEXT NOT NULL,
            trigger_name TEXT NOT NULL,
            trigger_group TEXT NOT NULL,
            instance_name TEXT NOT NULL,
            fired_time BIGINT NOT NULL,
            sched_time BIGINT NOT NULL,
            priority INTEGER NOT NULL,
            state TEXT NOT NULL,
            job_name TEXT NULL,
            job_group TEXT NULL,
            is_nonconcurrent BOOL NOT NULL,
            requests_recovery BOOL NULL,
            PRIMARY KEY (sched_name, entry_id)
        );
        CREATE TABLE IF NOT EXISTS qrtz_scheduler_state (
            sched_name TEXT NOT NULL,
            instance_name TEXT NOT NULL,
            last_checkin_time BIGINT NOT NULL,
            checkin_interval BIGINT NOT NULL,
            PRIMARY KEY (sched_name, instance_name)
        );
        CREATE TABLE IF NOT EXISTS qrtz_locks (
            sched_name TEXT NOT NULL,
            lock_name TEXT NOT NULL,
            PRIMARY KEY (sched_name, lock_name)
        );
        CREATE INDEX IF NOT EXISTS idx_qrtz_j_req_recovery ON qrtz_job_details(requests_recovery);
        CREATE INDEX IF NOT EXISTS idx_qrtz_t_next_fire_time ON qrtz_triggers(next_fire_time);
        CREATE INDEX IF NOT EXISTS idx_qrtz_t_state ON qrtz_triggers(trigger_state);
        CREATE INDEX IF NOT EXISTS idx_qrtz_t_nft_st ON qrtz_triggers(next_fire_time, trigger_state);
        CREATE INDEX IF NOT EXISTS idx_qrtz_ft_trig_name ON qrtz_fired_triggers(trigger_name);
        CREATE INDEX IF NOT EXISTS idx_qrtz_ft_trig_group ON qrtz_fired_triggers(trigger_group);
        CREATE INDEX IF NOT EXISTS idx_qrtz_ft_trig_nm_gp ON qrtz_fired_triggers(sched_name, trigger_name, trigger_group);
        CREATE INDEX IF NOT EXISTS idx_qrtz_ft_trig_inst_name ON qrtz_fired_triggers(instance_name);
        CREATE INDEX IF NOT EXISTS idx_qrtz_ft_job_name ON qrtz_fired_triggers(job_name);
        CREATE INDEX IF NOT EXISTS idx_qrtz_ft_job_group ON qrtz_fired_triggers(job_group);
        CREATE INDEX IF NOT EXISTS idx_qrtz_ft_job_req_recovery ON qrtz_fired_triggers(requests_recovery);
        """;
}
