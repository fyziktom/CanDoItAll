# B05 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## B05-T01 — Inject Docker host execution dependencies

- [x] Remove direct LocalWorkspaceProcessHost construction. Consume B01 process host, resolver, environment policy, workspace scope, registry, and receipt primitives.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T02 — Separate Docker executable and daemon capability

- [x] Probe executable, context/config, daemon/socket/remote endpoint, authorization, and recipe-specific capability without passing arbitrary Docker environment; project it through the asynchronous runtime executor catalog and execution gate.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T03 — Harden Docker environment and paths

- [x] Use OS/tool-specific environment names with host case semantics; validate config/root and scheme-specific endpoint paths through Core C4 contracts, reject endpoint credentials, and redact normalized protected values.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T04 — Produce FileTools compatibility report

- [x] Test the exact direct-source development identity on Windows and Ubuntu headless; compile unvalidated package-mode desktop launching fail-closed, cover interactive Linux/macOS profiles deterministically, and retain actual macOS as operator-deferred.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T05 — Quarantine or upgrade unsupported FileTools behavior

- [x] If package support is missing or unsafe, disable the capability truthfully or create a separate package issue/change; do not reimplement its internals opportunistically in this bundle.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T06 — Make desktop actions host-bound and optional

- [x] Use Core host-bound application preferences, desktop-session capability, and explicit enablement. Service/headless profiles must not attempt GUI launch.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T07 — Create external dependency ledger/probes

- [x] For every plugin/native dependency record version, source, supported OS/profile, probe, permissions, failure mode, remediation, and test evidence.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T08 — Run plugin/desktop failure matrix

- [x] Cover missing Docker, denied/socket/link endpoint, remote host, indeterminate Docker inventory, missing desktop session, foreign executable preference, unvalidated package mode, timeout, final pre-delegation cancellation, and link-safe path open.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T09 — Issue integration gate R3b

- [x] Proceed to Processes only after optional integrations degrade independently and no duplicate process/path/secret stack remains.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [x] Every owned requirement has evidence and status.
- [x] Focused validation and required stable regression pass.
- [x] Source references/findings/ADRs/traceability are current.
- [x] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
