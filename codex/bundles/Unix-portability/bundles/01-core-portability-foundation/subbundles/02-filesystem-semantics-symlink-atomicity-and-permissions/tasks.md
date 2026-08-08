# A02 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## A02-T01 — Separate logical and physical comparers

- [ ] Use ordinal semantics for logical keys. Introduce a root/volume-aware physical-path policy that does not assume all macOS volumes or all Unix hosts share one case model.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T02 — Make enumeration deterministic

- [ ] Audit every Directory.Enumerate*/GetFiles result that influences persistence, hashing, planning, receipts, migrations, tool selection, or tests. Sort by normalized logical key with explicit tie-breaking.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T03 — Unify link containment policy

- [ ] Define managed-root rules for symlinks, reparse points, junctions, missing leaves, and existing ancestors. Apply the same security contract to read, write, open, migrate, and process-target validation.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T04 — Harden file operations against races

- [ ] Minimize check/use gaps, revalidate parent identity immediately before mutation, keep temporary files in the target directory, and document any residual TOCTOU risk that cannot be eliminated with managed APIs.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T05 — Create atomic persistence primitives

- [ ] Implement tested temporary-write, flush, replace, backup, cleanup, and cancellation semantics for small JSON/key/catalog files and large storage payloads where appropriate.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T06 — Add bounded cross-process coordination

- [ ] Protect control-plane, key/vault, and catalog generation changes with a process-safe lock or transactional authority. Prove contention, stale/recovery, timeout, and cancellation behavior.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T07 — Apply Unix ownership and modes

- [ ] Create private directories and files with the required modes, verify actual modes after move/migration, and refuse to use secret-bearing paths when hardening fails.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T08 — Define portable physical filenames

- [ ] Replace host invalid-character behavior with an application policy, collision handling, reserved-name handling, length limits, Unicode normalization decision, and preserved display names.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T09 — Make watchers convergent

- [ ] Treat events as hints, deduplicate/debounce by generation, schedule deterministic rescan/fingerprint after overflow/error, and provide a polling fallback for unsupported/unreliable roots.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A02-T10 — Run malicious and failure-injection fixtures

- [ ] Test link swaps, case collisions, concurrent writers, process crash, full disk/permission failure where feasible, watcher overflow, rename storms, and cleanup boundaries.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
