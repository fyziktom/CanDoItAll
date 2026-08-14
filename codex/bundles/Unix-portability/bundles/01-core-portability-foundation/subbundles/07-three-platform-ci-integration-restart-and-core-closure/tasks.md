# A07 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## A07-T01 — Restore an active CI workflow

- [x] Create required Windows, Ubuntu, and macOS restore/build/stable-test jobs. Keep shell usage portable and cache only deterministic dependency inputs.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A07-T02 — Add actual-host core portability tests

- [ ] Run path, filesystem, storage, permission, secret-provider selection, control-plane, and headless startup tests on the real host OS.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

Windows and Ubuntu are complete; genuine macOS is operator-deferred under `HOSTED-PORTABILITY-VALIDATION-001`.

## A07-T03 — Prove migrations and restart

- [ ] Exercise old logical paths, host-bound records, legacy Data Protection/key fixtures, new vault records, interrupted migration, restart, and rollback.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

Windows/Ubuntu migration and restart coverage is complete; genuine macOS restart is operator-deferred. Keychain actual-host proof is separately deferred.

## A07-T04 — Publish and run outside the checkout

- [ ] Create clean RID artifacts and start them with explicit temporary/service roots. Assert no dependency on repository-relative writable state or global user caches.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

Windows and Ubuntu outside-checkout runs pass; genuine macOS is operator-deferred.

## A07-T05 — Add static portability/security guards

- [x] Fail CI on unowned OS branches, raw shared Windows path defaults, insecure secret fallback, unsafe absolute-path persistence, or unclassified scan findings.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A07-T06 — Run Windows regression and core UI smoke

- [x] Preserve the stable Windows gate and a minimal browser/readiness smoke with runtime/desktop features disabled.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A07-T07 — Perform independent architecture/security/operations review

- [x] Review support claims, migration rollback, permissions, key protection, capability truthfulness, and residual risks.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

Independent local-readiness review is GO in `reviews/21-a07-independent-review.md`. Final C4 review is deferred until merge/release validation.

## A07-T08 — Issue Core Gate C4 and handoff

- [x] Record the exact pushed implementation anchors, local evidence, support matrix, open limitations, and source delta that B00 must revalidate.
- [x] Record the operator-deferred hosted/macOS validation boundary without claiming C4 GO.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
