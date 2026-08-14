# A91 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## A91-T01 — Stop all destructive cleanup

- [ ] Preserve old key rings, DPAPI payloads, vault generations, database backups, and migration journals.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A91-T02 — Classify committed generations

- [ ] Identify source/destination/provider/key IDs without logging values.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A91-T03 — Restore read capability on the source host

- [ ] Use the original authorized Windows/profile context where DPAPI or old key protection requires it.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A91-T04 — Resume or roll back transactionally

- [ ] Verify every record before pointer commit; clean orphans only after independent confirmation.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A91-T05 — Produce a redacted incident/recovery report

- [ ] Include root cause, affected records count, proof, residual risk, and prevention tests.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
