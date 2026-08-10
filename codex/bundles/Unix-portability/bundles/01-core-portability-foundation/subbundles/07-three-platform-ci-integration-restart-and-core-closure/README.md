# A07 — Three-platform CI, integration, restart, and Core Gate C4

## Mission

Create durable Windows/Linux/macOS evidence and a versioned handoff anchor for the runtime/tools/process bundle.

## Why now

Portable source changes are not support until active actual-host CI proves build, storage, secrets, migration, restart, and headless startup.

## Scope

- Execute only the tasks and requirements owned by this subbundle.
- Update affected source references, findings, requirements, ADRs, validation, and evidence.
- Preserve established architecture and migration compatibility.

## Out of scope

- Downstream subbundle implementation.
- Opportunistic unrelated cleanup.
- Changes to external repositories/packages unless this subbundle explicitly invokes a split/quarantine path.
- Commit, push, or PR publication without explicit operator instruction.

## Source hotspots

- `{{REPO_ROOT}}/.github/workflows/ci.yml`
- `{{REPO_ROOT}}/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`
- `{{REPO_ROOT}}/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`
- `{{REPO_ROOT}}/tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj`

## Requirements

`CI-001`, `CI-002`, `CI-003`, `CI-004`, `CI-005`, `CI-006`, `CI-007`

## Prerequisites

- `A06`

## Deliverables

- Production and test changes limited to this scope.
- Failing-first or named characterization proof.
- Updated evidence and gate report.
- Updated source/finding/requirement traceability.
- Redaction scan result.
- Session handoff.

## Architecture constraints

- No broad platform service, duplicate process/path/secret stack, insecure fallback, automatic Unix elevation, or name-only process kill.
- Use logical versus physical path contracts correctly.
- Keep MAF generic and process semantics in `Processes`.
- Use typed process arguments; shell only for explicitly modeled scripts.
- Keep source-code comments in English.

## Entry gate

- Status before execution: `Eligible — Gate C3b/Hosting GO`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Core Gate C4 is GO on an exact commit with active Windows/Ubuntu/macOS evidence.
- All core P0 requirements are Solved and no critical finding remains open.
- Rollback and recovery have been rehearsed.
- Runtime bundle B00 is unblocked only against the C4 handoff anchor.

## Status

- `Local readiness GO — C4 pending exact-commit hosted Windows/Ubuntu/macOS evidence`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
