# A03 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## A03-T01 — Define purpose-specific application roots

- [x] Specify Windows, Linux XDG/service, and macOS Application Support/state/log roots with explicit overrides and fallback diagnostics. Keep workspace, control-plane, keys, logs, and temporary runtime data distinct.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A03-T02 — Version persisted path-bearing records

- [x] Add format/platform/host-affinity metadata where physical paths are unavoidable. Keep logical locators platform-neutral and migrate old separator forms.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A03-T03 — Implement foreign-path detection and rebind

- [x] Mark imported Windows/macOS/Linux absolute paths unresolved on foreign hosts. Provide safe operator/API workflows to choose a new root or application executable.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A03-T04 — Migrate storage references transactionally

- [x] Dry-run, back up, rewrite only known logical fields, verify content/revisions/tokens, and resume or roll back without deleting old data until commit.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A03-T05 — Migrate database-profile workspace roots

- [x] Preserve encrypted passwords and profile identity while updating/rebinding roots. Test profile switch, restart, failed migration, and old-version rollback.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A03-T06 — Migrate preferred application records

- [x] Bind preferences to platform/host, disable foreign executable paths, preserve extension policy, and keep desktop launch disabled by default.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A03-T07 — Harden FileSystemStorageDriver

- [x] Move SaveAsync and ReplaceAsync to the new atomic/cross-process/portable-filename primitives and preserve revision conflict semantics.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A03-T08 — Repair bootstrap authority

- [x] Resolve authoritative storage using canonical root identity and migration state rather than the current OS parsing a foreign string.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A03-T09 — Add operator backup/rollback and evidence

- [x] Produce redacted migration inventory, backup manifest, checksums, commit marker, rollback command, and post-restart verification.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A03-T10 — Issue storage migration gate C2a

- [x] Block secrets work if any path-bearing record can be silently reinterpreted, any backup is incomplete, or old Windows data cannot be read.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [x] Every owned requirement has evidence and status.
- [x] Focused validation and required stable regression pass.
- [x] Source references/findings/ADRs/traceability are current.
- [x] Artifacts are redacted.
- [x] Required independent reviewers record GO.
- [x] Handoff identifies the next eligible subbundle or conditional stop.
