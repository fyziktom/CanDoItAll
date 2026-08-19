# Agent prompt — A03 Storage, control-plane roots, and host-bound path migration

You are the senior C# architect and implementation agent for **CanDoItAll Core Portability Foundation**.

## Objective

Move storage and control-plane state onto the new path/filesystem contracts with transactional compatibility and rebind semantics.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `A03`.
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

- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Configuration/AppOptions.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/ControlPlanePaths.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/FileApplicationPreferences.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStorageDriver.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/Persistence/StorageBootstrapCatalogPolicy.cs`

## Tasks

- **A03-T01 — Define purpose-specific application roots:** Specify Windows, Linux XDG/service, and macOS Application Support/state/log roots with explicit overrides and fallback diagnostics. Keep workspace, control-plane, keys, logs, and temporary runtime data distinct.
- **A03-T02 — Version persisted path-bearing records:** Add format/platform/host-affinity metadata where physical paths are unavoidable. Keep logical locators platform-neutral and migrate old separator forms.
- **A03-T03 — Implement foreign-path detection and rebind:** Mark imported Windows/macOS/Linux absolute paths unresolved on foreign hosts. Provide safe operator/API workflows to choose a new root or application executable.
- **A03-T04 — Migrate storage references transactionally:** Dry-run, back up, rewrite only known logical fields, verify content/revisions/tokens, and resume or roll back without deleting old data until commit.
- **A03-T05 — Migrate database-profile workspace roots:** Preserve encrypted passwords and profile identity while updating/rebinding roots. Test profile switch, restart, failed migration, and old-version rollback.
- **A03-T06 — Migrate preferred application records:** Bind preferences to platform/host, disable foreign executable paths, preserve extension policy, and keep desktop launch disabled by default.
- **A03-T07 — Harden FileSystemStorageDriver:** Move SaveAsync and ReplaceAsync to the new atomic/cross-process/portable-filename primitives and preserve revision conflict semantics.
- **A03-T08 — Repair bootstrap authority:** Resolve authoritative storage using canonical root identity and migration state rather than the current OS parsing a foreign string.
- **A03-T09 — Add operator backup/rollback and evidence:** Produce redacted migration inventory, backup manifest, checksums, commit marker, rollback command, and post-restart verification.
- **A03-T10 — Issue storage migration gate C2a:** Block secrets work if any path-bearing record can be silently reinterpreted, any backup is incomplete, or old Windows data cannot be read.

## Exit

- Every path-bearing persisted record is logical or explicitly host-bound/versioned.
- Old Windows locators and profiles have proven migration/rebind/rollback.
- Storage writes and bootstrap authority use the new filesystem semantics.
- Gate C2a is GO and A04 may begin.
