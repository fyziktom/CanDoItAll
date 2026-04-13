# Corrective playbook — persistence and concurrency reset

## Status

- `Completed`
- `2026-04-13`: not triggered because Gate B passed without corrective work.

## Objective

- Repair any Gate B failure where save, publish, or transition flows remain unsafe because transactions, optimistic concurrency, or stable child identity are still incomplete.

## Covered Inputs

- `BRQ-006` Atomic save, publish, and transition behavior.
- `BRQ-008` Differential graph persistence.
- `BRQ-015` Regression and proof discipline.
- `BRQ-016` Repeated architecture review gates.
- `BRQ-017` Corrective-first continuation.

## Prerequisites

- Gate B or equivalent mutation-core proof has failed.
- Subbundles `05-06` were the most recent implemented phases being reviewed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Persistence.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Publication.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEnums.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEntities.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionEntityConfigurations.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeEntityConfigurations.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\SqliteWriteCoordinationIntegrationTests.cs
- C:\repositories\CanDoItAll\architecture_hardening_bundle\reviews\01-execution-report.md
- C:\repositories\CanDoItAll\architecture_hardening_bundle\reviews\02-architecture-gate-memo-log.md

## Deliverables

- A corrected mutation pipeline with explicit transaction and optimistic-concurrency safety.
- Repaired stable-identity handling where child graphs were still destructive or drift-prone.
- Updated integration proof and execution reporting tied to the Gate B rerun.

## Dependency Impact

- Publication, runtime, and query decomposition depend on Gate B being trustworthy.
- If this corrective path is weak, later refactors would sit on an unsafe persistence core.

## Validation Depth

- `Corrective critical foundation`

## Implementation Steps

1. Capture the failing Gate B proof and identify whether the defect is transactional, concurrency-related, identity-related, or migration-related.
2. Repair the smallest owned persistence surfaces that close the failure.
3. Rebuild the solution and rerun focused mutation-core integration proof.
4. Rerun any migration or provider-specific proof affected by the correction.
5. Rerun Gate B and update the execution report and gate memo before unblocking downstream work.

## Do Not Do

- Do not mask persistence defects with retries or fallback behavior.
- Do not accept provider-specific concurrency handling that leaves another provider weaker.
- Do not declare closure without stable-identity proof when graph persistence was involved.

## Acceptance Checklist

- Save, publish, and transition flows are transaction-safe for the corrected scope.
- Optimistic concurrency behavior is explicit and tested for the affected paths.
- Stable child identity is preserved where Gate B exposed drift.
- Gate B reruns and passes with fresh evidence.

## Proof Required

- Solution build.
- Focused integration tests for save, publish, transition, and write-coordination behavior.
- Migration or provider proof when schema or persistence configuration changed.
- Updated `reviews/01-execution-report.md` and `reviews/02-architecture-gate-memo-log.md`.

## Browser Validation Logging

- This corrective path is usually non-UI. Record `N/A` unless the failure also affected a visible workspace save or publish interaction that needs browser confirmation.

## Progression Gate

- Gate B passes with explicit conflict-handling, transaction, and stable-identity proof, allowing subbundle `08` to resume.

## Suggested Agent Prompt

```text
Execute only the persistence-and-concurrency corrective subbundle for a failed Gate B. Repair the unsafe mutation core, rerun build and focused integration proof, rerun Gate B, and keep downstream phases blocked until it passes.
```
