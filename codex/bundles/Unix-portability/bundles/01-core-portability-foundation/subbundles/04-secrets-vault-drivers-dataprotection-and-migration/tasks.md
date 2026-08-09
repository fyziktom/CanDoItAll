# A04 tasks

## Entry checklist

- [x] Verify exact checkout and preserve unrelated working-tree changes.
- [x] Verify prerequisite gate evidence.
- [x] Reproduce focused baseline/characterization.
- [x] Confirm every source hotspot after materialization.

## A04-T01 — Separate provider selection from provider claims

- [x] Probe required native service/configuration at startup. Auto must select only a proven provider; unsupported, locked, unavailable, or headless states produce distinct diagnostics.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T02 — Implement/prove macOS secure storage

- [x] Add a Keychain-backed adapter or approved secure alternative behind the existing abstraction. Define item identity, update/delete, access scope, user interaction, concurrency, restart, and backup expectations.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk. Actual macOS execution remains a Gate C2 decision input.

## A04-T03 — Implement/prove Linux interactive secure storage

- [x] Add a Secret Service adapter with explicit D-Bus/session/locked-service behavior. Do not silently fall back when no graphical/session keyring exists.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T04 — Design the headless secure provider

- [x] Select and implement an explicit certificate, remote-vault, or externally supplied wrapping-key profile. It must work for service accounts without an interactive keyring and must fail closed if missing.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T05 — Protect the ASP.NET Data Protection key ring

- [x] Choose platform/profile-specific at-rest protection whose bootstrap does not depend on that ring. Keep file permissions and rotation separate from cryptographic protection.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T06 — Retire the insecure file-vault production path

- [x] Remove plaintext Base64 key generation from Auto/production, version legacy payloads, and provide an explicit development/test-only or migration disposition.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T07 — Build one migration state machine

- [x] Inventory legacy DPAPI, legacy Data Protection secret payloads, control-plane database passwords, and vault references. Stage destination, verify read, commit reference, retain source until checkpoint, then clean idempotently.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T08 — Implement Windows-side DPAPI migration

- [x] Decrypt DPAPI only on an authorized Windows host and export/re-encrypt through the selected portable provider. Include dry-run, backup, interruption, resume, and rollback.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T09 — Add atomicity, locking, modes, versioning, and rotation

- [x] Use A02 primitives for key/payload generations; verify restrictive modes; test concurrent startup, key rotation interruption, orphan cleanup, and old-generation retention.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T10 — Prove redaction and recovery

- [x] Seed sentinel secrets through success and every failure path; scan logs, exceptions, receipts, CI artifacts, migration reports, and backups for leakage.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T11 — Issue Security Gate C2

- [ ] Require independent security review plus actual Windows/Linux/macOS/headless restart and migration evidence before platform composition continues. Independent review recorded NO-GO; Windows, Linux, headless, rollback-remediation, and scanner-remediation proof are complete, but actual macOS remains unavailable.
- [x] Add failing-first test or named characterization evidence.
- [x] Record changed files, design decision, commands, results, evidence, and residual risk.

## A04-T12 — Restore truthful basic local first-launch behavior

- [x] Use built-in DPAPI/`Strong` for Windows `Auto`; add a Unix basic local file vault that requires no interactive or external vault, preserves the existing local file format, enforces `0700`/`0600`, and reports a typed weaker protection level plus non-secret same-user warning.
- [x] Keep DPAPI, Keychain, Secret Service, and external wrapping-key providers available as stronger explicit profiles; do not silently label the basic provider as equivalent protection.
- [x] Preserve explicit legacy `DataProtectionFile` development compatibility while production misuse remains rejected.
- [x] Prove Windows and Linux first launch, restart/read continuity, Unix file modes, strong-provider selection, negative production policy, and artifact non-disclosure with explicit scanner coverage accounting.
- [x] Obtain independent remediation review before reconsidering Gate C2. SEC-014 decision: GO.

## Closure checklist

- [x] Every owned requirement has evidence and status.
- [x] Focused validation and required stable regression pass for reopened SEC-014.
- [x] Source references/findings/ADRs/traceability are current.
- [x] Artifacts are redacted.
- [ ] Required independent reviewers record GO. Current decision: NO-GO pending actual macOS Keychain proof.
- [x] Handoff reflects SEC-014 closure and the remaining genuine macOS Keychain condition. A05 remains ineligible until C2 GO.
