# Retention Cleanup Policy

## Status

- `Completed`

## Objective

- Add explicit retention cleanup for operational Cognitive Memory records with dry-run behavior and conservative data-safety boundaries.

## Covered Inputs

- CM-P1-003
- CM-P1-007

## Prerequisites

- API contract versioning subbundle passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Operations\CognitiveMemoryOperationalContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Operations\CognitiveMemoryScheduledAutomationRunner.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallEntities.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedEntities.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Consolidation\CognitiveMemoryConsolidationEntities.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.OperationsEndpoints.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryOperationalServicesTests.cs

## Deliverables

- Typed retention policy request/result contracts.
- Explicit cleanup application service.
- API endpoint or operator path for dry-run and execution.
- Tests proving dry-run counts and execute behavior.

## Dependency Impact

- Operator audit surface can show cleanup results after this service exists.

## Validation Depth

- Data-safety gate.

## Implementation Steps

1. Identify safe operational records and FK deletion order.
2. Add a conservative service with dry-run default.
3. Register service and expose through API.
4. Add tests for dry-run and execute.

## Do Not Do

- Do not delete canonical memory records, claims, evidence anchors, source manifests, source items, or projection state by default.
- Do not add background cleanup without operator intent.

## Acceptance Checklist

- Cleanup policy requires an explicit cutoff.
- Dry-run does not mutate.
- Execute removes only targeted operational records.
- Result includes actionable counts.

## Proof Required

- Unit or integration tests.
- Web build.
- Docs update.

## Proof Captured

- `ICognitiveMemoryRetentionCleanupService` and `/api/cognitive-memory[/v1]/retention/cleanup` are implemented with dry-run-first request semantics.
- Each cleanup call records a durable `CognitiveMemoryRunKind.RetentionCleanup` run for operator audit visibility.
- Unit tests prove dry-run counts without mutation and execute deletes only eligible operational records.
- `docs/cognitive-memory/operations/retention-cleanup.md` documents scopes and canonical-data exclusions.

## Browser Validation Logging

- Browser proof is required only if cleanup controls are added to Blazor UI.

## Progression Gate

- Continue only after cleanup behavior is conservative, typed, and tested.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Add explicit retention cleanup contracts/service/API with dry-run proof, protect canonical memory data, update the bundle proof, and stop if deletion order is ambiguous.
```
