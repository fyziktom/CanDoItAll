# A92 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## A92-T01 — Freeze writes and capture manifests

- [ ] Record catalogs, database rows, filesystem tree, checksums, links, modes, and migration journal.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A92-T02 — Classify logical versus physical corruption

- [ ] Do not reinterpret arbitrary backslashes or foreign absolute paths.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A92-T03 — Restore from backup or reconstruct by verified content identity

- [ ] Keep original files until references and revisions are reconciled.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A92-T04 — Repair host bindings and authority selection

- [ ] Require explicit operator rebind for ambiguous roots.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A92-T05 — Add regression fixtures and re-run migration gate

- [ ] Update the migration state machine before resuming normal work.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
