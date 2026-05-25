using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.SchedulerPlanner;

public static class SchedulerPlannerSchemaInitializer
{
    public static async Task EnsureAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (!dbContext.Database.IsRelational())
        {
            return;
        }

        var providerName = dbContext.Database.ProviderName ?? string.Empty;
        if (providerName.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await dbContext.Database.ExecuteSqlRawAsync(PostgreSqlSchema, cancellationToken);
            return;
        }

        throw new NotSupportedException($"Scheduler planner schema initialization only supports PostgreSQL relational providers. Provider '{providerName}' is not supported.");
    }

    private const string PostgreSqlSchema =
        """
        CREATE TABLE IF NOT EXISTS "SchedulerPlanner_Plans" (
            "Id" uuid NOT NULL CONSTRAINT "PK_SchedulerPlanner_Plans" PRIMARY KEY,
            "Name" character varying(180) NOT NULL,
            "Description" text NOT NULL,
            "TargetKind" integer NOT NULL,
            "TargetId" uuid NOT NULL,
            "TargetVersionId" uuid NULL,
            "TargetNameSnapshot" character varying(240) NOT NULL,
            "CronExpression" character varying(160) NOT NULL,
            "CronDescription" character varying(500) NOT NULL,
            "TimeZoneId" character varying(120) NOT NULL,
            "MisfirePolicy" integer NOT NULL,
            "IsEnabled" boolean NOT NULL,
            "StartAtUtc" timestamp with time zone NULL,
            "EndAtUtc" timestamp with time zone NULL,
            "InputJson" text NOT NULL,
            "AutomationTriggerId" uuid NOT NULL,
            "AutomationTriggerKey" character varying(180) NOT NULL,
            "NextPlannedFireAtUtc" timestamp with time zone NULL,
            "LastFiredAtUtc" timestamp with time zone NULL,
            "LastError" text NOT NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            "UpdatedAtUtc" timestamp with time zone NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_SchedulerPlanner_Plans_AutomationTriggerId" ON "SchedulerPlanner_Plans" ("AutomationTriggerId");
        CREATE INDEX IF NOT EXISTS "IX_SchedulerPlanner_Plans_TargetKind_TargetId_IsEnabled" ON "SchedulerPlanner_Plans" ("TargetKind", "TargetId", "IsEnabled");
        CREATE INDEX IF NOT EXISTS "IX_SchedulerPlanner_Plans_NextPlannedFireAtUtc" ON "SchedulerPlanner_Plans" ("NextPlannedFireAtUtc");
        CREATE TABLE IF NOT EXISTS "SchedulerPlanner_Runs" (
            "Id" uuid NOT NULL CONSTRAINT "PK_SchedulerPlanner_Runs" PRIMARY KEY,
            "PlanId" uuid NOT NULL,
            "DedupeKey" character varying(260) NOT NULL,
            "AutomationEnvelopeId" uuid NOT NULL,
            "CorrelationId" uuid NULL,
            "FiredAtUtc" timestamp with time zone NOT NULL,
            "Status" integer NOT NULL,
            "AttemptCount" integer NOT NULL,
            "TargetRunId" uuid NULL,
            "TargetRunKind" character varying(80) NOT NULL,
            "Summary" text NOT NULL,
            "ErrorMessage" text NOT NULL,
            "DispatchedAtUtc" timestamp with time zone NULL,
            "CreatedAtUtc" timestamp with time zone NOT NULL,
            "UpdatedAtUtc" timestamp with time zone NOT NULL,
            CONSTRAINT "FK_SchedulerPlanner_Runs_SchedulerPlanner_Plans_PlanId" FOREIGN KEY ("PlanId") REFERENCES "SchedulerPlanner_Plans" ("Id") ON DELETE CASCADE
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_SchedulerPlanner_Runs_DedupeKey" ON "SchedulerPlanner_Runs" ("DedupeKey");
        CREATE INDEX IF NOT EXISTS "IX_SchedulerPlanner_Runs_PlanId_FiredAtUtc" ON "SchedulerPlanner_Runs" ("PlanId", "FiredAtUtc");
        """;
}
