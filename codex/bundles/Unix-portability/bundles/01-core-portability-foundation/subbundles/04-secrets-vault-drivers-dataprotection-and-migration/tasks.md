# A04 tasks

## Entry checklist

- [ ] Verify exact checkout and preserve unrelated working-tree changes.
- [ ] Verify prerequisite gate evidence.
- [ ] Reproduce focused baseline/characterization.
- [ ] Confirm every source hotspot after materialization.

## A04-T01 — Separate provider selection from provider claims

- [ ] Probe required native service/configuration at startup. Auto must select only a proven provider; unsupported, locked, unavailable, or headless states produce distinct diagnostics.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T02 — Implement/prove macOS secure storage

- [ ] Add a Keychain-backed adapter or approved secure alternative behind the existing abstraction. Define item identity, update/delete, access scope, user interaction, concurrency, restart, and backup expectations.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T03 — Implement/prove Linux interactive secure storage

- [ ] Add a Secret Service adapter with explicit D-Bus/session/locked-service behavior. Do not silently fall back when no graphical/session keyring exists.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T04 — Design the headless secure provider

- [ ] Select and implement an explicit certificate, remote-vault, or externally supplied wrapping-key profile. It must work for service accounts without an interactive keyring and must fail closed if missing.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T05 — Protect the ASP.NET Data Protection key ring

- [ ] Choose platform/profile-specific at-rest protection whose bootstrap does not depend on that ring. Keep file permissions and rotation separate from cryptographic protection.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T06 — Retire the insecure file-vault production path

- [ ] Remove plaintext Base64 key generation from Auto/production, version legacy payloads, and provide an explicit development/test-only or migration disposition.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T07 — Build one migration state machine

- [ ] Inventory legacy DPAPI, legacy Data Protection secret payloads, control-plane database passwords, and vault references. Stage destination, verify read, commit reference, retain source until checkpoint, then clean idempotently.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T08 — Implement Windows-side DPAPI migration

- [ ] Decrypt DPAPI only on an authorized Windows host and export/re-encrypt through the selected portable provider. Include dry-run, backup, interruption, resume, and rollback.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T09 — Add atomicity, locking, modes, versioning, and rotation

- [ ] Use A02 primitives for key/payload generations; verify restrictive modes; test concurrent startup, key rotation interruption, orphan cleanup, and old-generation retention.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T10 — Prove redaction and recovery

- [ ] Seed sentinel secrets through success and every failure path; scan logs, exceptions, receipts, CI artifacts, migration reports, and backups for leakage.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T11 — Issue Security Gate C2

- [ ] Require independent security review plus actual Windows/Linux/macOS/headless restart and migration evidence before platform composition continues.
- [ ] Add failing-first test or named characterization evidence.
- [ ] Record changed files, design decision, commands, results, evidence, and residual risk.

## Closure checklist

- [ ] Every owned requirement has evidence and status.
- [ ] Focused validation and required stable regression pass.
- [ ] Source references/findings/ADRs/traceability are current.
- [ ] Artifacts are redacted.
- [ ] Required independent reviewers record GO.
- [ ] Handoff identifies the next eligible subbundle or conditional stop.
