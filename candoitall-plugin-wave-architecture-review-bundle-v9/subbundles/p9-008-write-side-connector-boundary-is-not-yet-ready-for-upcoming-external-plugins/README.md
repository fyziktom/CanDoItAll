# P9-008 — Write-side connector boundary is not yet ready for upcoming external plugins

Severity: **High**  
Gate: **MG-01**  
Module area: **Workbench / Connector platform**

## Problem
Email, LinkedIn, and custom API plugins will introduce real external side effects. Without a generic connector command / outbox / retry / idempotency boundary, those plugins will likely couple UI/domain actions directly to external calls.

## Required architectural end-state
Before shipping write-side plugins, introduce a generic connector command boundary with durable queueing, idempotency keys, retry/backoff, audit history, and optional approval hooks.

## Primary evidence
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchCrossModuleMutations.cs` lines 29-123: Current durable mutation model is scoped to internal project mutation records.
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchCrossModuleMutationService.cs` lines 45-187: Current orchestration covers delete/move side effects, not a general connector command/outbox boundary.
