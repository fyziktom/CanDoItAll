# Transaction, concurrency, and conflict hardening

## Status

- `Ready`

## Objective

- Add explicit transaction boundaries, provider-agnostic optimistic concurrency, and clean conflict translation for the critical Process mutation flows.

## Covered Inputs

- `U003` DB conflict risk and long-term stabilization concerns.
- `BRQ-006` Atomic save, publish, and transition.
- `BRQ-007` Provider-agnostic optimistic concurrency.
- `F004` Missing optimistic concurrency.
- `analysis/05-db-concurrency-and-runtime-risk-review.md`.

## Prerequisites

- `04-architecture-review-gate-a` passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeEntityConfigurations.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Persistence.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Publication.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\AppDbContext.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\Migrations\AppDbContextModelSnapshot.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\Migrations\AppDbContextModelSnapshot.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\SqliteWriteCoordinationIntegrationTests.cs

## Deliverables

- Application-managed concurrency tokens on the required process aggregates.
- Explicit transaction boundaries for save, publish, and critical runtime transitions.
- Deterministic translation of concurrency and uniqueness conflicts into the module’s result/error contract.
- Targeted conflict tests using multiple contexts.

## Dependency Impact

- Subbundles 06-10 depend on this safety net.
- If aggregate-level conflict handling is missing, differential persistence and runtime extraction will remain unsafe.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Select and implement a provider-agnostic concurrency-token pattern for the required aggregates.
2. Update EF configuration and both provider snapshots/migrations as needed.
3. Wrap critical mutation flows in explicit transactions with clear rollback behavior.
4. Translate `DbUpdateConcurrencyException` and relevant uniqueness conflicts into explicit module-level conflict results.
5. Add multi-context integration tests that intentionally create conflicts.

## Scope Exceptions

- This phase does not yet implement full differential graph persistence; it hardens the safety rails first.
- Search-index eventual consistency is not the primary target unless it is directly affected by the transaction changes.

## Do Not Do

- Do not rely on SQLite write coordination as a substitute for aggregate concurrency protection.
- Do not introduce a SQL Server-specific rowversion strategy.
- Do not swallow raw DB exceptions without translating their meaning.

## Acceptance Checklist

- Critical process aggregates use provider-agnostic optimistic concurrency.
- Save/publish/transition flows are wrapped in explicit transactions.
- Conflict failures return explicit domain/result meaning instead of raw exceptions.
- Multi-context conflict tests exist and pass.

## Proof Required

- Build proof if the model changes.
- Integration tests proving conflict handling and rollback behavior.
- Snapshot/migration coherence for both providers when the model changes.

## Browser Validation Logging

- N/A.

## Progression Gate

- Aggregate-level conflict protection is visible, transaction boundaries are explicit, and targeted conflict tests prove the module no longer relies on silent last-write-wins behavior.

## Suggested Agent Prompt

```text
Implement only subbundle 05. Add provider-agnostic optimistic concurrency and explicit transactions to save, publish, and critical runtime transitions. Translate conflicts into the module’s result/error pattern, add multi-context integration tests, and stop before differential graph persistence.
```
