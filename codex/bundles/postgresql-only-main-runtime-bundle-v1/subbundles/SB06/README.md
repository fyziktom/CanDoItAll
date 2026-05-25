# SB06 - Tune Processes, Workflows, Automation, and Outbox for PostgreSQL

## Objective

Use PostgreSQL-only runtime assumptions to improve process/workflow/automation/outbox correctness and concurrency.

## Audit areas

```text
src/CanDoItAll.Modules.Processes/**
src/CanDoItAll.Modules.Automation/**
src/CanDoItAll.Modules.Plugins/**
src/CanDoItAll.Infrastructure/BackgroundJobs/**
src/CanDoItAll.Infrastructure/Persistence/**
tests/**/*Process*.cs
tests/**/*Workflow*.cs
tests/**/*Automation*.cs
tests/**/*Outbox*.cs
```

## Required analysis

Find any logic that was serialized or simplified because SQLite had to work.

Look for:

- sequential-only execution assumptions,
- low concurrency constants,
- non-atomic claim/update patterns,
- duplicate execution risks,
- missing lock/lease expiration,
- missing idempotency keys,
- optimistic-only patterns that are insufficient for multi-worker runtime.

## Recommended PostgreSQL patterns

Use centralized abstractions instead of scattered raw SQL when possible.

Possible patterns:

- `FOR UPDATE SKIP LOCKED` for queue/outbox/process item claiming.
- Durable lease columns.
- Attempt counters and retry-after timestamps.
- Idempotency keys for external effects.
- Dead-letter or failed state for unrecoverable items.

## Required tests

Add or update PostgreSQL-backed tests proving:

- Two workers cannot claim the same item.
- Expired lease can be recovered.
- Completed item is not re-executed.
- Retry preserves idempotency.
- Worker concurrency is not artificially serialized by previous SQLite constraints.

## Validation

```powershell
dotnet test .\CanDoItAll.slnx --filter "Process|Workflow|Automation|Outbox"
rg -n -i "FOR UPDATE SKIP LOCKED|locked_until|lease|claim|outbox|idempot" src tests
```

## Required proof

```text
proof/SB06/manifest.md
proof/SB06/semantic-invariants.md
evidence/SB06/concurrency-test-results.log
evidence/SB06/process-workflow-audit.md
```
