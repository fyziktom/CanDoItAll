# processes-hardening-followup-api-docs-governance-v7

## Status

Prepared for Codex execution.

## Purpose

Review and harden the CanDoItAll `Processes` runtime after the phase6 implementation. The current branch has made real progress on persisted operation contracts, operation-aware tool policy, artifact lineage, typed block states, and Blazor template migration. This follow-up targets the gaps that still risk unnecessary process blocking, false completion, stale API/tool behavior, or documentation drift.

## Branch context

- Repository: `fyziktom/CanDoItAll`
- Reviewed branch available through GitHub connector: `processes-hardening`
- User referred to `process-hardening`; verify branch naming locally before execution.
- Reviewed head: `phase6` / `ca898eccf32664b60e996bf806a035067675c11e`
- PostgreSQL-only requirement remains active. Do not add SQLite runtime paths or SQLite migrations.

## High-level finding

The runtime implementation is stronger, but the public/API/tool surface and skills/docs are likely not fully synchronized with the new typed process governance model. The next work must ensure that `Processes` API tools, import/export, templates, skills, docs, and runtime validation all agree on the same typed contracts.

## Execution rule

Execute subbundles in order. After every 3–4 subbundles run the relevant refactor checkpoint before continuing. Do not skip API/tool/skill documentation work; it is now part of the correctness surface.
