# A02 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## A02-T01 — Separate logical and physical comparers

- [x] Use ordinal semantics for logical keys. Introduce a root/volume-aware physical-path policy that does not assume all macOS volumes or all Unix hosts share one case model.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T02 — Make enumeration deterministic

- [x] Audit every Directory.Enumerate*/GetFiles result that influences persistence, hashing, planning, receipts, migrations, tool selection, or tests. Sort by normalized logical key with explicit tie-breaking.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T03 — Unify link containment policy

- [x] Define managed-root rules for symlinks, reparse points, junctions, missing leaves, and existing ancestors. Apply the same security contract to read, write, open, migrate, and process-target validation.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T04 — Harden file operations against races

- [x] Minimize check/use gaps, revalidate parent identity immediately before mutation, keep temporary files in the target directory, and document any residual TOCTOU risk that cannot be eliminated with managed APIs.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T05 — Create atomic persistence primitives

- [x] Implement tested temporary-write, flush, replace, backup, cleanup, and cancellation semantics for small JSON/key/catalog files and large storage payloads where appropriate.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T06 — Add bounded cross-process coordination

- [x] Protect control-plane, key/vault, and catalog generation changes with a process-safe lock or transactional authority. Prove contention, stale/recovery, timeout, and cancellation behavior.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T07 — Apply Unix ownership and modes

- [x] Create private directories and files with the required modes, verify actual modes after move/migration, and refuse to use secret-bearing paths when hardening fails.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T08 — Define portable physical filenames

- [x] Replace host invalid-character behavior with an application policy, collision handling, reserved-name handling, length limits, Unicode normalization decision, and preserved display names.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T09 — Make watchers convergent

- [x] Treat events as hints, deduplicate/debounce by generation, schedule deterministic rescan/fingerprint after overflow/error, and provide a polling fallback for unsupported/unreliable roots.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T10 — Run malicious and failure-injection fixtures

- [x] Test link swaps, case collisions, concurrent writers, process crash, full disk/permission failure where feasible, watcher overflow, rename storms, and cleanup boundaries.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [x] Every owned requirement has evidence and status.
- [x] Focused validation and required stable regression pass.
- [x] Source references/findings/ADRs/traceability are current.
- [x] Artifacts are redacted or classified as synthetic test vectors by fingerprint.
- [x] Required independent reviewers record GO.
- [x] Handoff identifies the next eligible subbundle or conditional stop.
