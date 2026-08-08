# A06 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## A06-T01 — Define supported core profiles

- [ ] Separate headless Web host support from optional desktop/runtime claims. State database, secret backend, architecture/RID, and external dependency prerequisites.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A06-T02 — Publish clean artifacts

- [ ] Prove framework-dependent win-x64, linux-x64, osx-x64, and osx-arm64 publishes outside the repository. Do not add trimming, single-file, or self-contained changes without separate evidence.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A06-T03 — Create Linux service/runbook

- [ ] Define service user, XDG/data/control-plane roots, environment file, PostgreSQL dependency/readiness, systemd hardening, logs, restart, upgrade, backup, and rollback.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A06-T04 — Create macOS service/runbook

- [ ] Define interactive and launchd/headless profiles, Application Support/state/log roots, Keychain or headless provider requirements, restart, upgrade, backup, and rollback.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A06-T05 — Refactor installation boundaries

- [ ] Keep the existing Windows PowerShell installer working. Share publish/config generation where safe; implement Unix entry scripts or a small .NET installer without duplicating security/root logic.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A06-T06 — Add redacted diagnostics and health

- [ ] Expose bounded platform/root/provider/capability state, health/readiness, and support profile. Avoid secret values and minimize full absolute paths.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A06-T07 — Update developer/operator docs

- [ ] Replace universal Windows assumptions; document Linux/macOS setup, migrations, limitations, Docker/PostgreSQL, permissions, service profiles, and troubleshooting.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A06-T08 — Rehearse clean install/start/restart/rollback

- [ ] Use a clean user/service account and artifact directory, not the repository checkout; preserve logs and redacted evidence.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
