# A06 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## A06-T01 — Define supported core profiles

- [x] Separate headless Web host support from optional desktop/runtime claims. State database, secret backend, architecture/RID, and external dependency prerequisites.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A06-T02 — Publish clean artifacts

- [x] Prove framework-dependent win-x64, linux-x64, osx-x64, and osx-arm64 publishes outside the repository. Do not add trimming, single-file, or self-contained changes without separate evidence.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A06-T03 — Create Linux service/runbook

- [x] Define service user, XDG/data/control-plane roots, environment file, PostgreSQL dependency/readiness, systemd hardening, logs, restart, upgrade, backup, and rollback.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A06-T04 — Create macOS service/runbook

- [x] Define interactive and launchd/headless profiles, Application Support/state/log roots, Keychain or headless provider requirements, restart, upgrade, backup, and rollback. Keep actual-host support explicitly unverified pending A07.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A06-T05 — Refactor installation boundaries

- [x] Keep the existing Windows PowerShell installer working. Share publish/config generation where safe; implement Unix entry scripts or a small .NET installer without duplicating security/root logic.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A06-T06 — Add redacted diagnostics and health

- [x] Expose bounded platform/root/provider/capability state, health/readiness, and support profile. Avoid secret values and minimize full absolute paths.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A06-T07 — Update developer/operator docs

- [x] Replace universal Windows assumptions; document Linux/macOS setup, migrations, limitations, Docker/PostgreSQL, permissions, service profiles, and troubleshooting.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A06-T08 — Rehearse clean install/start/restart/rollback

- [x] Use a clean user/service account and artifact directory, not the repository checkout; preserve logs and redacted evidence.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [x] Every owned requirement has evidence and status.
- [x] Focused validation and required stable regression pass.
- [x] Source references/findings/ADRs/traceability are current.
- [x] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [x] Handoff identifies the next eligible subbundle or conditional stop.
