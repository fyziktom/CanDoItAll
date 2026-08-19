using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class CorrectProcessPlanHashClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                WITH payloads AS
                (
                    SELECT "PlanId", "PayloadJson"::jsonb AS payload
                    FROM process_instance_plans
                ),
                classified AS
                (
                    SELECT "PlanId",
                        CASE
                            WHEN NOT (
                                jsonb_path_exists(payload, '$.**.hostProfileId') OR
                                jsonb_path_exists(payload, '$.**.hostCapabilities') OR
                                jsonb_path_exists(payload, '$.**.requiredHostCapabilities') OR
                                jsonb_path_exists(payload, '$.**.requiredRuntimeToolNames'))
                                THEN 'LegacyV1'
                            WHEN
                                jsonb_typeof(payload) = 'object' AND
                                jsonb_typeof(payload #> '{driverStack}') = 'object' AND
                                jsonb_typeof(payload #> '{driverStack,hostProfileId}') = 'object' AND
                                jsonb_typeof(payload #> '{driverStack,hostProfileId,value}') = 'string' AND
                                length(btrim(payload #>> '{driverStack,hostProfileId,value}')) > 0 AND
                                jsonb_typeof(payload #> '{driverStack,hostCapabilities}') = 'array' AND
                                CASE
                                    WHEN jsonb_typeof(payload #> '{driverStack,drivers}') = 'array'
                                    THEN NOT EXISTS
                                    (
                                        SELECT 1
                                        FROM jsonb_array_elements(payload #> '{driverStack,drivers}') AS driver
                                        WHERE jsonb_typeof(driver) IS DISTINCT FROM 'object'
                                           OR jsonb_typeof(driver -> 'requiredHostCapabilities') IS DISTINCT FROM 'array'
                                    )
                                    ELSE false
                                END AND
                                CASE
                                    WHEN jsonb_typeof(payload -> 'steps') = 'array'
                                    THEN NOT EXISTS
                                    (
                                        SELECT 1
                                        FROM jsonb_array_elements(payload -> 'steps') AS step
                                        WHERE jsonb_typeof(step) IS DISTINCT FROM 'object'
                                           OR jsonb_typeof(step -> 'requiredHostCapabilities') IS DISTINCT FROM 'array'
                                           OR jsonb_typeof(step -> 'requiredRuntimeToolNames') IS DISTINCT FROM 'array'
                                           OR NOT (
                                                jsonb_typeof(step -> 'executionStrategyBinding') = 'null'
                                                OR (
                                                    jsonb_typeof(step -> 'executionStrategyBinding') = 'object'
                                                    AND jsonb_typeof(step #> '{executionStrategyBinding,hostProfileId}') = 'object'
                                                    AND jsonb_typeof(step #> '{executionStrategyBinding,hostProfileId,value}') = 'string'
                                                    AND length(btrim(step #>> '{executionStrategyBinding,hostProfileId,value}')) > 0
                                                    AND jsonb_typeof(step #> '{executionStrategyBinding,hostCapabilities}') = 'array'
                                                )
                                           )
                                    )
                                    ELSE false
                                END AND
                                jsonb_typeof(payload -> 'strategies') = 'object' AND
                                CASE
                                    WHEN jsonb_typeof(payload #> '{strategies,executionBindings}') = 'array'
                                    THEN NOT EXISTS
                                    (
                                        SELECT 1
                                        FROM jsonb_array_elements(payload #> '{strategies,executionBindings}') AS binding
                                        WHERE jsonb_typeof(binding) IS DISTINCT FROM 'object'
                                           OR jsonb_typeof(binding -> 'hostProfileId') IS DISTINCT FROM 'object'
                                           OR jsonb_typeof(binding #> '{hostProfileId,value}') IS DISTINCT FROM 'string'
                                           OR length(btrim(binding #>> '{hostProfileId,value}')) = 0
                                           OR jsonb_typeof(binding -> 'hostCapabilities') IS DISTINCT FROM 'array'
                                    )
                                    ELSE false
                                END AND
                                CASE
                                    WHEN jsonb_typeof(payload #> '{strategies,managerBindings}') = 'array'
                                    THEN NOT EXISTS
                                    (
                                        SELECT 1
                                        FROM jsonb_array_elements(payload #> '{strategies,managerBindings}') AS binding
                                        WHERE jsonb_typeof(binding) IS DISTINCT FROM 'object'
                                           OR jsonb_typeof(binding -> 'hostProfileId') IS DISTINCT FROM 'object'
                                           OR jsonb_typeof(binding #> '{hostProfileId,value}') IS DISTINCT FROM 'string'
                                           OR length(btrim(binding #>> '{hostProfileId,value}')) = 0
                                           OR jsonb_typeof(binding -> 'hostCapabilities') IS DISTINCT FROM 'array'
                                    )
                                    ELSE false
                                END AND
                                CASE
                                    WHEN jsonb_typeof(payload #> '{strategies,recoveryBindings}') = 'array'
                                    THEN NOT EXISTS
                                    (
                                        SELECT 1
                                        FROM jsonb_array_elements(payload #> '{strategies,recoveryBindings}') AS binding
                                        WHERE jsonb_typeof(binding) IS DISTINCT FROM 'object'
                                           OR jsonb_typeof(binding -> 'hostProfileId') IS DISTINCT FROM 'object'
                                           OR jsonb_typeof(binding #> '{hostProfileId,value}') IS DISTINCT FROM 'string'
                                           OR length(btrim(binding #>> '{hostProfileId,value}')) = 0
                                           OR jsonb_typeof(binding -> 'hostCapabilities') IS DISTINCT FROM 'array'
                                    )
                                    ELSE false
                                END AND
                                CASE
                                    WHEN jsonb_typeof(payload #> '{strategies,resupplyBindings}') = 'array'
                                    THEN NOT EXISTS
                                    (
                                        SELECT 1
                                        FROM jsonb_array_elements(payload #> '{strategies,resupplyBindings}') AS binding
                                        WHERE jsonb_typeof(binding) IS DISTINCT FROM 'object'
                                           OR jsonb_typeof(binding -> 'hostProfileId') IS DISTINCT FROM 'object'
                                           OR jsonb_typeof(binding #> '{hostProfileId,value}') IS DISTINCT FROM 'string'
                                           OR length(btrim(binding #>> '{hostProfileId,value}')) = 0
                                           OR jsonb_typeof(binding -> 'hostCapabilities') IS DISTINCT FROM 'array'
                                    )
                                    ELSE false
                                END AND
                                jsonb_typeof(payload -> 'manager') = 'object' AND
                                (
                                    jsonb_typeof(payload #> '{manager,managerStrategyBinding}') = 'null'
                                    OR (
                                        jsonb_typeof(payload #> '{manager,managerStrategyBinding}') = 'object'
                                        AND jsonb_typeof(payload #> '{manager,managerStrategyBinding,hostProfileId}') = 'object'
                                        AND jsonb_typeof(payload #> '{manager,managerStrategyBinding,hostProfileId,value}') = 'string'
                                        AND length(btrim(payload #>> '{manager,managerStrategyBinding,hostProfileId,value}')) > 0
                                        AND jsonb_typeof(payload #> '{manager,managerStrategyBinding,hostCapabilities}') = 'array'
                                    )
                                ) AND
                                CASE
                                    WHEN jsonb_typeof(payload #> '{manager,recoveryBindings}') = 'array'
                                    THEN NOT EXISTS
                                    (
                                        SELECT 1
                                        FROM jsonb_array_elements(payload #> '{manager,recoveryBindings}') AS binding
                                        WHERE jsonb_typeof(binding) IS DISTINCT FROM 'object'
                                           OR jsonb_typeof(binding -> 'hostProfileId') IS DISTINCT FROM 'object'
                                           OR jsonb_typeof(binding #> '{hostProfileId,value}') IS DISTINCT FROM 'string'
                                           OR length(btrim(binding #>> '{hostProfileId,value}')) = 0
                                           OR jsonb_typeof(binding -> 'hostCapabilities') IS DISTINCT FROM 'array'
                                    )
                                    ELSE false
                                END AND
                                CASE
                                    WHEN jsonb_typeof(payload #> '{manager,resupplyBindings}') = 'array'
                                    THEN NOT EXISTS
                                    (
                                        SELECT 1
                                        FROM jsonb_array_elements(payload #> '{manager,resupplyBindings}') AS binding
                                        WHERE jsonb_typeof(binding) IS DISTINCT FROM 'object'
                                           OR jsonb_typeof(binding -> 'hostProfileId') IS DISTINCT FROM 'object'
                                           OR jsonb_typeof(binding #> '{hostProfileId,value}') IS DISTINCT FROM 'string'
                                           OR length(btrim(binding #>> '{hostProfileId,value}')) = 0
                                           OR jsonb_typeof(binding -> 'hostCapabilities') IS DISTINCT FROM 'array'
                                    )
                                    ELSE false
                                END
                                THEN 'HostCapabilitiesV2'
                            ELSE 'Unknown'
                        END AS payload_shape
                    FROM payloads
                )
                UPDATE process_instance_plans AS plans
                SET "PlanHashAlgorithmVersion" =
                        CASE classified.payload_shape
                            WHEN 'LegacyV1' THEN 'LegacyV1'
                            WHEN 'HostCapabilitiesV2' THEN 'HostCapabilitiesV2'
                            ELSE NULL
                        END,
                    "ExecutionState" =
                        CASE classified.payload_shape
                            WHEN 'LegacyV1' THEN 'NeedsRecompile'
                            WHEN 'HostCapabilitiesV2' THEN 'Executable'
                            ELSE 'Unknown'
                        END,
                    "MigrationReason" =
                        CASE classified.payload_shape
                            WHEN 'LegacyV1' THEN 'HostCapabilitiesWereNotSealed'
                            ELSE NULL
                        END
                FROM classified
                WHERE plans."PlanId" = classified."PlanId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
