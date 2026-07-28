# SB01 Proof Workspace

## Purpose

This directory is reserved for evidence produced while executing **Baseline discovery and 1.13 fixtures**.

## Rules

- Do not fabricate evidence.
- Record exact repository SHA, commands, exit codes, timestamps, and relevant environment details.
- Store failing-first and passing proof separately.
- Hash cross-version fixtures and any persisted-state payloads.
- Redact secrets and provider credentials.
- Update `reviews/01-execution-report.md` with links to the final evidence.

## Materialized Evidence

- `repository-head.txt` — immutable 1.13 baseline identity, drift, SDK/OS, and shared-working-tree classification.
- `discovery/` — raw discovery and classified-match output.
- `package-graph/` — direct/transitive 1.13 package graphs for all three owners.
- `build-and-test-baseline/` — successful direct-owner builds, passing slices, inherited failures, and runner hangs.
- `warning-baseline.txt` — warning and experimental-suppression inventory.
- `fixtures/maf-1.13/` — ten deterministic sanitized payloads, manifests, hashes, and explicit N/A/inactive classifications.
- `file-tool-inventory.json` — exact workspace-tool catalog and representative policy filters.
- `runtime-lifecycle.md` — managed port-5032 lifecycle and state authority.
- `rollback-consistency-boundary.md` — PostgreSQL/workspace/control-plane capture, rehearsal, and restore procedure.
- `artifact-validation.txt` — JSON parse, fixture-schema/hash, and tool-catalog validation.
- `a1-decision.md` — A1 `GO` with carried conditions and inherited-risk disclosures.
