# B05 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## B05-T01 — Inject Docker host execution dependencies

- [ ] Remove direct LocalWorkspaceProcessHost construction. Consume B01 process host, resolver, environment policy, workspace scope, registry, and receipt primitives.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T02 — Separate Docker executable and daemon capability

- [ ] Probe executable, context/config, daemon/socket/remote endpoint, authorization, and recipe-specific capability without passing arbitrary Docker environment.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T03 — Harden Docker environment and paths

- [ ] Use OS/tool-specific environment names with host case semantics; validate Docker config/root paths through Core C4 contracts and redact endpoint credentials.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T04 — Produce FileTools compatibility report

- [ ] Test package 0.1.18 on Windows, Ubuntu desktop/headless, macOS interactive/headless for open, reveal, preferred application, cancellation, unsupported state, and path safety.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T05 — Quarantine or upgrade unsupported FileTools behavior

- [ ] If package support is missing or unsafe, disable the capability truthfully or create a separate package issue/change; do not reimplement its internals opportunistically in this bundle.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T06 — Make desktop actions host-bound and optional

- [ ] Use Core host-bound application preferences, desktop-session capability, and explicit enablement. Service/headless profiles must not attempt GUI launch.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T07 — Create external dependency ledger/probes

- [ ] For every plugin/native dependency record version, source, supported OS/profile, probe, permissions, failure mode, remediation, and test evidence.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T08 — Run plugin/desktop failure matrix

- [ ] Cover missing Docker, denied socket, remote host, missing desktop session, foreign executable preference, unsupported package, timeout, cancellation, and link-safe path open.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## B05-T09 — Issue integration gate R3b

- [ ] Proceed to Processes only after optional integrations degrade independently and no duplicate process/path/secret stack remains.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
