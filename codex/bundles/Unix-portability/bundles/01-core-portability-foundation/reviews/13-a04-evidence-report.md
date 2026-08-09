# A04 evidence report

## Decision state

- Implementation: complete for A04 source scope.
- Evidence: complete on actual Windows, actual Linux Secret Service, and Windows/Linux headless external-key profiles; actual macOS remains unavailable.
- Gate result: C2 NO-GO after independent review and bounded re-review. Rollback and scanner findings are closed; genuine macOS evidence is the sole remaining blocker. A05 remains blocked.
- Source anchor: `a2856070e7303de077088fc7f2f7e96a5bcf0e70` on `unix-adoption`, plus the reviewed A01-A04 working tree.

## Design result

A04 replaces optimistic provider names and insecure production fallback with explicit, probed security profiles:

- `ISecretVaultCapability` reports typed provider availability. `SecretVaultStartupValidationHostedService` probes before serving and distinguishes unsupported OS, missing dependency, missing session, locked store, timeout, invalid configuration, and available state without exposing identifiers or values.
- Auto selects only Windows DPAPI, macOS Keychain, or Linux Secret Service. Interactive Unix providers reject headless operation; no Unix path silently falls back to a file vault.
- `MacOsKeychainSecretVault` uses the Security framework generic-password CRUD APIs with a scoped service identity. Contract tests cover create/read/update/delete, locked state, restart, concurrency, and headless rejection through an injected native client. Genuine macOS execution is still outstanding.
- `LinuxSecretServiceVault` invokes `secret-tool` using typed `ProcessStartInfo.ArgumentList`, redirected streams, no shell, bounded timeout, and explicit D-Bus/session/locked/dependency diagnostics. An actual Docker-hosted D-Bus plus GNOME Keyring session proves exact multiline/trailing-newline CRUD and restart behavior.
- `ExternalWrappingKeySecretVault` supports headless services on every host with a versioned AES-256-GCM envelope, authenticated key identity, current/previous generations, atomic private files, and externally supplied key bytes that are never persisted.
- The legacy `DataProtectionFile` vault and `InMemory` vault are available only in Development with explicit insecure opt-in. The legacy file vault has a separate read/delete-only migration source; Auto and production reject it.
- Data Protection key persistence uses a private durable XML repository. Windows Auto protects keys with DPAPI. Unix production requires an explicit current PFX certificate and may retain previous decryption certificates; PFX passwords come from named environment inputs. Unprotected key-ring persistence is Development-only. Bootstrap does not depend on secrets encrypted by that ring.
- `SecretMigrationCoordinator` provides one journaled state machine for legacy Data Protection payloads, DPAPI/file-vault sources, control-plane password continuity, and vault-reference destinations. Destination values are read back with fixed-time verification before pointer commit; sources remain until exact restart verification; cleanup and rollback are idempotent. Rollback verifies the source before publishing its reference, recognizes a verified already-restored reference on retry, and has deterministic post-save interruption coverage. Cross-process locking, source/selection drift, tamper, interruption, retry, orphan cleanup, and rollback are covered.
- Audit records and capability snapshots contain only typed states, counts, hashes, operation identifiers, and remediation. Physical secret values are absent from messages, journals, logs, and retained artifacts.

## Requirement evidence

| Requirement | Result | Principal proof |
|---|---|---|
| SEC-001 | Verified candidate | Auto mapping plus startup capability tests reject unsupported, locked, unavailable, dependency-missing, timeout, and headless states; no insecure fallback. |
| SEC-002 | Implementation verified; actual-host proof outstanding | Real Security-framework Keychain adapter and fake-native CRUD/update/delete/restart/concurrency/locked/headless tests. Genuine macOS execution remains required by the independent gate decision. |
| SEC-003 | Verified candidate | Actual Linux D-Bus/GNOME Keyring/`secret-tool` CRUD and restart; unit coverage for missing session, locked service, missing dependency, timeout, and headless rejection. |
| SEC-004 | Verified candidate | External wrapping-key profile restart-tested on Windows and Linux; missing/invalid keys fail closed; key bytes are not stored beside ciphertext. |
| SEC-005 | Verified candidate | Actual Windows DPAPI key-ring proof; certificate-protected ring restart/rotation proof; private directory/file modes and atomic create-new repository tests. |
| SEC-006 | Verified candidate | Data Protection protector resolves solely from OS/profile startup input; scoped CodeAnalytics and source review find no vault/secret-resolution dependency in the bootstrap path. |
| SEC-007 | Verified candidate | Production rejects file/in-memory providers; Development requires explicit opt-in; legacy file data is exposed only through a read/delete migration source. |
| SEC-008 | Verified; independent remediation review closed | Actual Windows DPAPI migration dry-run, commit, injected forward and post-rollback-save interruption, resume, restart checkpoint, source cleanup, and idempotent rollback integration proof. |
| SEC-009 | Verified candidate | Golden legacy Data Protection payload and control-plane encrypted-password continuity checks pass through migration and restart. |
| SEC-010 | Verified candidate | Versioned authenticated envelopes, retained prior generations, A02 durable writes/private modes, cross-process journal lock, tamper/source-drift checks, interruption recovery, and orphan cleanup. |
| SEC-011 | Verified; independent remediation review closed | The schema-2 metadata-only scanner loaded four private sentinels and found zero sentinel leaks. Its 24 generic matches are six repeated synthetic unit-test fixtures, reviewed in `A04-secret-scan-classification.md`; scanner unit tests prove source text is never copied. |
| SEC-012 | Verified candidate | Capability/startup tests assert typed non-secret state and remediation; diagnostics omit key IDs, logical secret keys, paths, and values. |
| SEC-013 | Verified; independent remediation review closed | Redacted migration audit sink plus idempotent checkpoint cleanup and rollback tests, including source-verification failure, post-reference-save interruption, retry, destination-change, and source-preservation behavior. |

## Final commands and results

| Host | Scope | Result | Evidence |
|---|---|---:|---|
| Windows | Full Unit Release regression | 5,524/5,524 | `artifacts/unix-portability/A04/windows/A04-windows-full-unit-remediation-authoritative.trx` |
| Windows | Security/Data Protection/migration Unit Release filter | 94/94 | `artifacts/unix-portability/A04/windows/A04-windows-security-unit-remediation.trx` |
| Windows | `Category=SecretPortability|Category=SecretMigration` integration | 4/4 | `artifacts/unix-portability/A04/windows/A04-windows-security-integration-remediation.trx` |
| Windows | Web Release build | process exit 0; no warning/error hits | `artifacts/unix-portability/A04/windows/A04-windows-web-release-remediation.log` |
| Linux Docker | Security/Data Protection/migration Unit Release filter | 94/94 | `artifacts/unix-portability/A04/linux/A04-linux-security-unit-remediation.trx` |
| Linux Docker | Portable secret/migration integration | 4/4 | `artifacts/unix-portability/A04/linux/A04-linux-security-integration-remediation.trx` |
| Linux Docker | Actual D-Bus + GNOME Keyring Secret Service | 1/1 | `artifacts/unix-portability/A04/linux/A04-linux-secret-service-remediation-final.trx` |
| Linux Docker | Web Release build | process exit 0; no warning/error hits | `artifacts/unix-portability/A04/linux/A04-linux-web-release-remediation.log` |

Linux used Docker Engine `linux 29.6.2`, `mcr.microsoft.com/dotnet/sdk:10.0`, `dbus-run-session`, `gnome-keyring-daemon`, and `secret-tool`. The actual provider test exercised the current source in the container and preserved exact multiline/trailing-newline content across a new vault instance.

## Failure evidence and remediation

- Named characterization tests capture the prepared-state failures: Auto returned unsupported Unix providers, the production file vault generated a Base64 key beside ciphertext, and Data Protection persisted unprotected Unix XML.
- Initial Linux validation exposed a timeout exception whose provider state was not typed. The runner now classifies timeout distinctly and preserves only non-secret remediation.
- Migration review found that restart cleanup checked only destination presence. It now verifies the exact expected value before deleting a source.
- Journal review found that malformed or selection-changed authority state could be accepted. Schema, migration identity, timestamps, states, deterministic logical keys, duplicate entries, source kinds, and source references are now validated before mutation.
- Independent review found a rollback interruption gap between database reference save and journal advancement. Source readability is now verified before the save, already-restored source references resume idempotently, and an injected post-save interruption proves retry, source preservation, destination cleanup, and redacted audit behavior.
- Independent review constructively showed that scanner excerpts could reproduce adjacent values and that the required sentinel-file contract was missing. Scanner schema 2 now retains metadata/fingerprints only, accepts private sentinel files without scanning or echoing them, records effective limits/exclusions, and has dedicated disclosure regressions.
- A sandboxed Windows full-suite run could not access the real LocalApplicationData path. The unchanged exact run under the actual user profile passed 5,524/5,524; the sandbox result was environmental and is not presented as gate evidence.

Superseded working directories, copied source archives, and oversized diagnostic logs created during validation were removed only after resolving their exact paths. The retained final artifacts above are the authoritative proof set.

## Architecture evidence

- Scoped CodeAnalytics snapshot: `snap-20260809191620-b07bdd50`; four relevant projects, 678 types, 4,490 members, and 69 service registrations.
- Result: zero Error findings, zero Error diagnostics, and no blocking snapshot error. The 190 findings are 189 complexity observations plus one existing Infrastructure intra-project module cycle.
- Project direction remains `Infrastructure <- Modules.Security <- Composition/Web`. A04 adds no project-reference edge or abstraction project.
- Data Protection bootstrap remains in Infrastructure; OS/native vault adapters and migration orchestration remain in the Security module; composition selects concrete profiles and runs the startup validator.
- Detailed classification: `artifacts/unix-portability/A04/A04-static-audit-final.md`.

## Redaction evidence

`artifacts/unix-portability/A04/A04-secret-scan-final.json` scanned ten retained artifacts and reported 24 generic matches across the Windows full/focused and Linux focused Unit TRXs. All matches are six synthetic values repeated between TRX result and unit-definition test names for scanner/redaction negative tests. The report contains only rule/path/line metadata and truncated fingerprints, never excerpts. The scanner loaded four private sentinels and reported zero exact sentinel findings. The classification is `artifacts/unix-portability/A04/A04-secret-scan-classification.md`.

## Residuals

- Genuine macOS Keychain execution is unavailable in the current environment. Docker cannot simulate Darwin/Security.framework. The explicit SEC-002/A04-T11 condition requires actual Keychain proof at C2, so A05 stays blocked.
- Linux proof uses an ephemeral GNOME Keyring session in the .NET SDK container. Production distributions still require the documented D-Bus session and Secret Service dependency.
- External wrapping-key security depends on the operator supplying the named environment input from a protected service secret/mount and retaining previous generations during rotation.
- PFX backup, certificate issuance, password rotation, and host deployment remain operator responsibilities; the application validates configured current/previous certificates and fails closed.
- Existing large security/control-plane files are analyzer hotspots. A04 keeps the state machine and native-provider responsibilities cohesive instead of creating cross-boundary abstractions during security migration.

## Review result

Independent Security Gate C2 review and its bounded re-review are recorded in `reviews/14-a04-independent-review.md`. SEC-008/SEC-013 rollback recovery and SEC-011 scanner non-disclosure are closed. Genuine macOS SEC-002/A04-T11 proof is the sole remaining blocker and prevents C2 GO and A05 entry.
