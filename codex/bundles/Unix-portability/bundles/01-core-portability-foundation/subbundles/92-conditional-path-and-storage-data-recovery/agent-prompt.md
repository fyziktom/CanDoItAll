# Agent prompt — A92 Conditional path and storage data recovery

You are the senior C# architect and implementation agent for **CanDoItAll Core Portability Foundation**.

## Objective

Recover control-plane/storage state after incorrect path conversion, host rebind, partial move, or catalog corruption.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `A92`.
- Verify HEAD and dirty state before edits.
- Use CodeAnalytics/solution analysis where available before broad changes.
- Add failing-first tests or named characterization evidence.
- Prefer existing owners and narrow ports; do not create a parallel framework.
- Preserve Windows behavior and existing data.
- Run focused and stable gates; use actual Windows/Linux/macOS hosts when required.
- Update bundle evidence and stop on every NO-GO.
- Keep all source-code comments in English.
- Do not commit, push, or open a PR unless explicitly instructed.

## Source hotspots

- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStorageDriver.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageBootstrapCatalogPolicy.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs`

## Tasks

- **A92-T01 — Freeze writes and capture manifests:** Record catalogs, database rows, filesystem tree, checksums, links, modes, and migration journal.
- **A92-T02 — Classify logical versus physical corruption:** Do not reinterpret arbitrary backslashes or foreign absolute paths.
- **A92-T03 — Restore from backup or reconstruct by verified content identity:** Keep original files until references and revisions are reconciled.
- **A92-T04 — Repair host bindings and authority selection:** Require explicit operator rebind for ambiguous roots.
- **A92-T05 — Add regression fixtures and re-run migration gate:** Update the migration state machine before resuming normal work.

## Exit

- Catalog and physical content agree.
- No trusted-root escape or silent data loss remains.
- The failed storage gate is re-reviewed.
