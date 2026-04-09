# PRM-F15 — Storage, migrations, and performance hardening

## Objective

Use the shared CanDoItAll app database first, add strong indexing and retention boundaries, and leave a clean seam for later extraction of hot append-only stores.

## Priority and wave

- Priority: **Critical**
- Planned wave: **Cross-cutting**
- Depends on: **PRM-F01, PRM-F02, PRM-F07, PRM-F08**

## Why this feature exists

This feature is part of the first process-management bundle because the user explicitly wants process definitions, actor responsibility, handoffs, and interactive modeling to land **before** the intelligence lake and before deep runtime coupling to the AgentFramework overlay.

## In scope

- Process tables live in the main app database with consistent naming and indexing conventions.
- SQLite remains supported for local users without extra setup.
- PostgreSQL migrations exist and stay in lockstep with SQLite.
- The journal and runtime tables have a defined retention/extraction seam for future scale.

## Non-goals

- Do not optimize prematurely by extracting storage before the process model is proven.
- Do not let SQLite and PostgreSQL drift.

## Primary repo touchpoints

- `src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs`
- `src/CanDoItAll.Migrations.Sqlite/*`
- `src/CanDoItAll.Migrations.PostgreSql/*`
- `tests/CanDoItAll.Tests.Integration/* database profile coverage`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.DatabaseProfiles.cs`
