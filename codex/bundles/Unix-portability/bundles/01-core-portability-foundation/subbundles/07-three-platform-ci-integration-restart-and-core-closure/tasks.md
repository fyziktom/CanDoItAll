# A07 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## A07-T01 — Restore an active CI workflow

- [ ] Create required Windows, Ubuntu, and macOS restore/build/stable-test jobs. Keep shell usage portable and cache only deterministic dependency inputs.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A07-T02 — Add actual-host core portability tests

- [ ] Run path, filesystem, storage, permission, secret-provider selection, control-plane, and headless startup tests on the real host OS.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A07-T03 — Prove migrations and restart

- [ ] Exercise old logical paths, host-bound records, legacy Data Protection/key fixtures, new vault records, interrupted migration, restart, and rollback.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A07-T04 — Publish and run outside the checkout

- [ ] Create clean RID artifacts and start them with explicit temporary/service roots. Assert no dependency on repository-relative writable state or global user caches.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A07-T05 — Add static portability/security guards

- [ ] Fail CI on unowned OS branches, raw shared Windows path defaults, insecure secret fallback, unsafe absolute-path persistence, or unclassified scan findings.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A07-T06 — Run Windows regression and core UI smoke

- [ ] Preserve the stable Windows gate and a minimal browser/readiness smoke with runtime/desktop features disabled.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A07-T07 — Perform independent architecture/security/operations review

- [ ] Review support claims, migration rollback, permissions, key protection, capability truthfulness, and residual risks.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A07-T08 — Issue Core Gate C4 and handoff

- [ ] Record the exact passing commit, CI run links, artifact checksums, support matrix, open limitations, and source delta that B00 must revalidate.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
