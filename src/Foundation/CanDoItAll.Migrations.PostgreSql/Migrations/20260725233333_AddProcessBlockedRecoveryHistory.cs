using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddProcessBlockedRecoveryHistory : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long?>(
                name: "AppliedSequence",
                table: "process_strategy_result_receipts",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockedRecoveryActionsJson",
                table: "process_runtime_states",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE process_receipt_application_candidates
                ON COMMIT DROP
                AS
                SELECT DISTINCT
                    receipt."RunId",
                    receipt."StepInstanceId",
                    receipt."StrategyId",
                    receipt."IdempotencyKey",
                    application."GlobalSequence" AS "ApplicationGlobalSequence",
                    application."RootSequence" AS "ApplicationRootSequence"
                FROM public.process_strategy_result_receipts AS receipt
                INNER JOIN public.process_runtime_states AS state
                    ON state."RunId" = receipt."RunId"
                INNER JOIN public.process_dispatch_claims AS claim
                    ON claim."RunId" = receipt."RunId"
                    AND claim."StepInstanceId" = receipt."StepInstanceId"
                    AND claim."ResultIdempotencyKey" = receipt."IdempotencyKey"
                    AND claim."Status" = CASE receipt."Outcome"
                        WHEN 'Canceled' THEN 'Cancelled'
                        ELSE 'Completed'
                    END
                INNER JOIN public.process_runtime_events AS completion
                    ON completion."RootRunId" = state."RootRunId"
                    AND completion."RunId" = receipt."RunId"
                    AND completion."EventType" = 'DispatchClaimCompleted'
                    AND completion."PayloadHash" = claim."ClaimToken"::text
                INNER JOIN public.process_runtime_events AS application
                    ON application."RootRunId" = completion."RootRunId"
                    AND application."RootSequence" = completion."RootSequence" + 1
                    AND application."RunId" = completion."RunId"
                    AND application."CorrelationId" = completion."CorrelationId"
                    AND application."OccurredAtUtc" = completion."OccurredAtUtc"
                    AND application."ActorKind" = completion."ActorKind"
                    AND application."ActorId" = completion."ActorId"
                    AND application."SchemaVersion" = completion."SchemaVersion"
                    AND application."Sensitivity" = completion."Sensitivity"
                    AND application."CausationId" IS NOT DISTINCT FROM completion."CausationId"
                    AND application."PayloadHash" = receipt."ResultHash"
                    AND application."EventType" = CASE receipt."AppliedStepStatus"
                        WHEN 'Completed' THEN 'StepCompleted'
                        WHEN 'Failed' THEN 'StepFailed'
                        WHEN 'Ready' THEN 'StepReady'
                        WHEN 'Blocked' THEN 'StepBlocked'
                        WHEN 'Cancelled' THEN 'StepCancelled'
                        ELSE NULL
                    END;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM public.process_strategy_result_receipts AS receipt
                        LEFT JOIN process_receipt_application_candidates AS candidate
                            ON candidate."RunId" = receipt."RunId"
                            AND candidate."StepInstanceId" = receipt."StepInstanceId"
                            AND candidate."StrategyId" = receipt."StrategyId"
                            AND candidate."IdempotencyKey" = receipt."IdempotencyKey"
                        GROUP BY
                            receipt."RunId",
                            receipt."StepInstanceId",
                            receipt."StrategyId",
                            receipt."IdempotencyKey"
                        HAVING COUNT(candidate."ApplicationGlobalSequence") <> 1
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23514',
                            MESSAGE = 'Cannot assign process result application order: a receipt has no unique result-application event.';
                    END IF;

                    IF EXISTS (
                        SELECT 1
                        FROM process_receipt_application_candidates AS candidate
                        GROUP BY candidate."ApplicationGlobalSequence"
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23514',
                            MESSAGE = 'Cannot assign process result application order: one result-application event maps to multiple receipts.';
                    END IF;
                END
                $$;

                WITH ordered_receipts AS (
                    SELECT
                        candidate."RunId",
                        candidate."StepInstanceId",
                        candidate."StrategyId",
                        candidate."IdempotencyKey",
                        ROW_NUMBER() OVER (
                            PARTITION BY candidate."RunId"
                            ORDER BY
                                candidate."ApplicationRootSequence",
                                candidate."StepInstanceId",
                                candidate."StrategyId",
                                candidate."IdempotencyKey"
                        ) AS "AppliedSequence"
                    FROM process_receipt_application_candidates AS candidate
                )
                UPDATE public.process_strategy_result_receipts AS target
                SET "AppliedSequence" = ordered."AppliedSequence"
                FROM ordered_receipts AS ordered
                WHERE target."RunId" = ordered."RunId"
                    AND target."StepInstanceId" = ordered."StepInstanceId"
                    AND target."StrategyId" = ordered."StrategyId"
                    AND target."IdempotencyKey" = ordered."IdempotencyKey";

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM public.process_strategy_result_receipts
                        WHERE "AppliedSequence" IS NULL
                    ) THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '23514',
                            MESSAGE = 'Process result application-order backfill left unmatched receipts.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.AlterColumn<long>(
                name: "AppliedSequence",
                table: "process_strategy_result_receipts",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_strategy_result_receipts_RunId_AppliedSequence",
                table: "process_strategy_result_receipts",
                columns: new[] { "RunId", "AppliedSequence" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_process_strategy_result_receipts_RunId_AppliedSequence",
                table: "process_strategy_result_receipts");

            migrationBuilder.DropColumn(
                name: "AppliedSequence",
                table: "process_strategy_result_receipts");

            migrationBuilder.DropColumn(
                name: "BlockedRecoveryActionsJson",
                table: "process_runtime_states");
        }
    }
}
