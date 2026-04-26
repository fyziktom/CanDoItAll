# PostgreSQL process validation proof

## Status

- `Completed`

## Objective

Validate the prompt, agent, and process-template changes through PostgreSQL-backed process execution, starting with deterministic checks and then attempting a real-agent business-plan scenario.

## Covered Inputs

- `N006`: Atomic then handoff then real validation.
- `N007`: Use PostgreSQL, not SQLite.

## Prerequisites

- Subbundles 01, 02, and 03 completed.

## Exact Source References

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Support\PostgresTestAvailability.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Support\CanDoItAllTestEnvironment.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesMcpStdioIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessOutboxIntegrationTests.cs`
- `C:\repositories\CanDoItAll\docker-compose.yml`

## Deliverables

- PostgreSQL availability check result.
- Targeted process tests using PostgreSQL profile.
- Mock-agent or deterministic process handoff validation for expected artifacts.
- Real-agent business-plan scenario attempt with completed proof or explicit provider/runtime blocker.

## Dependency Impact

- This is the final process proof. Weak proof keeps the bundle open.

## Validation Depth

- `Process-critical closure`

## Implementation Steps

1. Run deterministic targeted tests after subbundles 01-03.
2. Confirm PostgreSQL availability through existing support.
3. Run targeted process validation with PostgreSQL profile.
4. Attempt real-agent scenario if provider configuration is available.
5. Record commands, results, blockers, and raw-note closure.

## Scope Exceptions

- If real provider credentials are unavailable, do not fabricate success; record the exact blocker after PostgreSQL-backed deterministic validation passes.

## Do Not Do

- Do not run only SQLite or in-memory tests for process validation.
- Do not treat a mock-agent run as a real-agent run.
- Do not close the bundle if the expected business-plan artifacts cannot be produced or validated.

## Acceptance Checklist

- PostgreSQL validation command ran or PostgreSQL unavailability is explicitly recorded.
- Atomic prompt/seed/template tests passed first.
- Process flow proof checks expected artifact shape and handoff information.
- Real-agent attempt is recorded honestly.

## Proof Required

- Command output summaries in `reviews/01-execution-report.md`.
- PostgreSQL provider profile evidence.
- Real-agent run ID/session evidence or blocker details.

## Completion Proof

- Improved PostgreSQL availability to use a local project-default PostgreSQL service before Docker compose.
- Added `BusinessPlanProcessPostgresIntegrationTests`, which creates an isolated PostgreSQL database, imports/publishes/runs the business-plan process, records required artifacts, selects the approved branch, completes the routed end step, and verifies the blocked path is skipped.
- Added opt-in `LiveSpecialistAgentScenarioIntegrationTests`, which creates an isolated PostgreSQL database and validates Business Strategist, Financial Strategist, and Marketing Specialist with real OpenAI-backed agent calls when `CANDOITALL_RUN_LIVE_AGENT_VALIDATION=true`.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~BusinessPlanProcessPostgresIntegrationTests" --no-restore` passed.
- `$env:CANDOITALL_RUN_LIVE_AGENT_VALIDATION='true'; dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~LiveSpecialistAgentScenarioIntegrationTests" --no-restore` passed.

## Browser Validation Logging

- N/A unless the real validation uses browser UI. If it does, record route, viewport, actions, screenshots, and result in the execution report.

## Progression Gate

- Final closure may proceed only after PostgreSQL-backed process validation and real-agent attempt are recorded.

## Suggested Agent Prompt

```text
Implement subbundle 04 only. Run PostgreSQL-backed process validation, attempt the real-agent scenario, and update the bundle proof honestly.
```
