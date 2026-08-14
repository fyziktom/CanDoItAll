# Agent prompt — A02 Filesystem semantics, symlink safety, atomicity, and permissions

You are the senior C# architect and implementation agent for **CanDoItAll Core Portability Foundation**.

## Objective

Create a trustworthy filesystem foundation for storage and key material on Windows, Linux, and macOS.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `A02`.
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

- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStoragePathPolicy.cs`
- `{{REPO_ROOT}}/src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/FileSystemStorageDriver.cs`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureLocalFileOpener.cs`
- `{{REPO_ROOT}}/tools/App/CanDoItAll.Manager/TailwindWatchSupervisorService.cs`

## Tasks

- **A02-T01 — Separate logical and physical comparers:** Use ordinal semantics for logical keys. Introduce a root/volume-aware physical-path policy that does not assume all macOS volumes or all Unix hosts share one case model.
- **A02-T02 — Make enumeration deterministic:** Audit every Directory.Enumerate*/GetFiles result that influences persistence, hashing, planning, receipts, migrations, tool selection, or tests. Sort by normalized logical key with explicit tie-breaking.
- **A02-T03 — Unify link containment policy:** Define managed-root rules for symlinks, reparse points, junctions, missing leaves, and existing ancestors. Apply the same security contract to read, write, open, migrate, and process-target validation.
- **A02-T04 — Harden file operations against races:** Minimize check/use gaps, revalidate parent identity immediately before mutation, keep temporary files in the target directory, and document any residual TOCTOU risk that cannot be eliminated with managed APIs.
- **A02-T05 — Create atomic persistence primitives:** Implement tested temporary-write, flush, replace, backup, cleanup, and cancellation semantics for small JSON/key/catalog files and large storage payloads where appropriate.
- **A02-T06 — Add bounded cross-process coordination:** Protect control-plane, key/vault, and catalog generation changes with a process-safe lock or transactional authority. Prove contention, stale/recovery, timeout, and cancellation behavior.
- **A02-T07 — Apply Unix ownership and modes:** Create private directories and files with the required modes, verify actual modes after move/migration, and refuse to use secret-bearing paths when hardening fails.
- **A02-T08 — Define portable physical filenames:** Replace host invalid-character behavior with an application policy, collision handling, reserved-name handling, length limits, Unicode normalization decision, and preserved display names.
- **A02-T09 — Make watchers convergent:** Treat events as hints, deduplicate/debounce by generation, schedule deterministic rescan/fingerprint after overflow/error, and provide a polling fallback for unsupported/unreliable roots.
- **A02-T10 — Run malicious and failure-injection fixtures:** Test link swaps, case collisions, concurrent writers, process crash, full disk/permission failure where feasible, watcher overflow, rename storms, and cleanup boundaries.

## Exit

- Gate C1 is GO after independent architecture/security review.
- Filesystem semantics are deterministic and actual-host tested.
- Managed-root link escape and unsafe permission cases fail closed.
- Atomic/cross-process behavior is proven before storage or secrets migration.
