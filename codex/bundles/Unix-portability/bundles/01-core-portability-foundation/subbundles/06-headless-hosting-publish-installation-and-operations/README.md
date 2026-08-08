# A06 — Headless hosting, publish, installation, and operations

## Mission

Turn code-level portability into repeatable Linux/macOS headless deployment and operator guidance without prematurely coupling to desktop runtime features.

## Why now

The current installed-web-app path is Windows/PowerShell oriented and no macOS/Linux service runbook proves application roots, permissions, restart, or rollback.

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

- `{{REPO_ROOT}}/tools/install/Install-CanDoItAllWebApp.ps1`
- `{{REPO_ROOT}}/src/App/CanDoItAll.Web/CanDoItAll.Web.csproj`
- `{{REPO_ROOT}}/src/App/CanDoItAll.Web/appsettings.json`
- `{{REPO_ROOT}}/docs/development-runtime.md`
- `{{REPO_ROOT}}/docs/operations/installed-web-app.md`

## Requirements

`HOST-001`, `HOST-002`, `HOST-003`, `HOST-004`, `HOST-005`, `DOC-001`

## Prerequisites

- `A05`

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

- Status before execution: `Blocked by A05`
- Verify exact HEAD, dirty state, prerequisites, and prior evidence.
- Reproduce the relevant baseline before edits.

## Exit gate

- Clean headless startup/restart succeeds on Windows, Ubuntu, and macOS.
- Publish/support claims are bounded to proven RIDs and profiles.
- Linux/macOS service and rollback runbooks are complete and rehearsed where required.
- Documentation no longer treats Windows behavior as universal.

## Status

- `Blocked by A05`

## Handoff

Record changed files, commands/results, evidence paths, design decisions, residual risks, and the next eligible subbundle. Stop on NO-GO.
