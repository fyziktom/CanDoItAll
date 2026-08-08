# A90 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## A90-T01 — Freeze downstream work

- [ ] Mark all dependent evidence invalid and stop later subbundles.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A90-T02 — Document the failed invariant

- [ ] Record exact source, dependency graph, reproduction, affected requirements, and why the current plan is unsafe.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A90-T03 — Choose the smallest owner-correct repair

- [ ] Prefer moving behavior to the existing owner or adding a narrow port over introducing a cross-cutting service.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A90-T04 — Add architecture characterization/failing tests

- [ ] Prove the defect and prevent recurrence.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A90-T05 — Implement and re-run the failed gate

- [ ] Update manifests/traceability and proceed only after independent GO.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
