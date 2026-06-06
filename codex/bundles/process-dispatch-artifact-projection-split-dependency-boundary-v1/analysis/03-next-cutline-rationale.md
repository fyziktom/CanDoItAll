# Next Cutline Rationale

## Decision

Do not extract Process Core yet.

## Rationale

The branch has made strong progress: MAF is decoupled from Processes, execution snapshots are process-owned, many dispatch partials were reduced, and artifact projection is now behind coordinators. However, the latest artifact projection boundary is still a transitional nested partial design.

The next necessary preparation step is to turn that transitional nested boundary into a real module-local boundary:

- one explicit orchestrator facade,
- one explicit context object,
- one explicit host/services dependency surface,
- one source-family coordinator per file,
- candidate state mutation as a testable helper,
- no hidden dependency on the full dispatch service.

This is a prerequisite for a later Process Core split because a future Core cannot safely depend on private methods hidden inside a Blazor/module service partial.

## Why not driver packs yet

The future driver idea still matters. Projection source families are very close to future driver evidence families, for example:

- execution artifact evidence,
- process mock evidence,
- workspace-written evidence,
- response-text evidence,
- browser evidence,
- completed-decision evidence.

But production driver APIs would be premature before the projection source-family vocabulary and context boundaries are stable. Keep driver readiness documentation-only in this bundle.
