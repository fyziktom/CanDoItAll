# Secrets and key bootstrap

## A04 implementation decision

A04 implements explicit provider profiles and makes provider capability a startup contract:

- Windows Auto selects DPAPI.
- macOS Auto selects Keychain and requires an interactive session.
- Linux Auto selects Secret Service and requires an available, unlocked D-Bus session keyring.
- Unix headless/service profiles explicitly select an external wrapping-key vault. The AES-256-GCM key is supplied by a protected environment source, is never written to the vault directory, and supports current plus retained previous key identifiers for rotation.
- The legacy Data Protection file vault and in-memory vault are rejected outside Development and require explicit insecure-development opt-in. The legacy file vault may otherwise be opened only through the read/delete migration-source boundary.

Every selected vault implements a capability probe. A hosted startup validator publishes only provider kind, availability state, and remediation; it prevents the application from serving when the configured provider is unsupported, unavailable, locked, dependency-missing, or incompatible with the current session/profile.

ASP.NET Data Protection uses a separate bootstrap policy. Windows Auto uses DPAPI. Unix production requires explicit certificate protection, with current and previous PFX certificates loaded directly from configured files and passwords supplied by named environment inputs. Unprotected persistence exists only as an explicit Development choice. The private XML repository uses the A02 durable writer, atomic create-new commits, and restrictive directory/file modes. No Data Protection bootstrap path resolves a secret encrypted by the same ring.

One migration coordinator handles legacy Data Protection payloads, DPAPI/file-vault sources, control-plane database-password continuity, and vault-reference destinations. It uses a private journal, cross-process lock, deterministic selection, destination read-back, optimistic source checks, restart verification, source cleanup after checkpoint, rollback, and redacted audit events. Rollback verifies a source before restoring its reference and treats an already-restored verified reference as an idempotent resume state. Malformed, tampered, source-changed, or selection-changed journals fail closed.

Actual Windows DPAPI and Linux Secret Service/headless restart proof is recorded in `reviews/13-a04-evidence-report.md`. The Keychain adapter has contract/fake-native coverage, but genuine macOS execution is not available in the current environment and remains an explicit gate input.

## Current mechanisms to preserve

1. DPAPI-protected legacy material on Windows.
2. ASP.NET Data Protection ciphertext for legacy `SecretRecord` values.
3. ASP.NET Data Protection ciphertext for control-plane database passwords.
4. New vault-reference records.
5. Current insecure file-vault payloads that may exist in development data.

## Provider profiles

### Windows interactive/service

- DPAPI remains a valid local provider when its scope matches the profile.
- Migration to another provider is explicit; do not break old DPAPI payloads.
- Data Protection ring may use DPAPI or another configured protector.

### macOS interactive

- Keychain-backed provider or approved secure alternative.
- Define whether access requires an unlocked user session.
- Headless/launchd must not pretend this profile is available.

### Linux interactive

- Secret Service with D-Bus/session/locked-state semantics.
- No silent file fallback when unavailable.

### Headless/service

Use an explicit provider independent of an interactive session, for example:

- certificate-protected local ring/vault;
- remote vault;
- externally supplied wrapping key from a protected secret mount/manager.

The implementation decision must consider local-first operation, deployment complexity, rotation, backup, and recovery. An environment value can bootstrap a wrapping key only when the operational profile treats the environment source as protected and does not log/persist it.

## Key-ring bootstrap

The Data Protection key ring must be available to decrypt existing records before those records can provide runtime secrets. Therefore:

- key-ring protection material is configured by OS/profile/startup input;
- it does not come only from a secret encrypted by the same ring;
- rotation retains old decrypting keys through migration;
- ring files have restrictive modes and atomic/cross-process writes.

## File vault disposition

The current Base64 key beside ciphertext is not production protection. Choose one:

- migrate to an OS/remote vault;
- keep a file envelope whose wrapping key is external and protected;
- retain only an explicit test/development provider with loud configuration and no production Auto selection.

Do not rename the current implementation and keep the same threat model.

## Migration

Source-side decryption happens only where authorized:

- DPAPI migration on Windows;
- legacy Data Protection migration with the old ring/protector;
- insecure file-vault migration with a protected backup copy.

Destination writes are staged and verified before database/reference pointer commit. Old data remains until restart verification and grace checkpoint.

## Redaction

Use sentinel secrets in tests. Scan:

- application/CI logs;
- exceptions;
- tool/process receipts;
- migration reports/journals;
- backups/manifests;
- screenshots/browser traces;
- generated portability scan excerpts.

A04's schema-2 scanner reports only metadata and fingerprints for reviewed synthetic secret-scanner/redaction fixtures embedded in TRX parameterized test names. It accepts private sentinel-file inputs without echoing or scanning them; all four seeded runtime/migration sentinels produced zero findings. See `artifacts/unix-portability/A04/A04-secret-scan-classification.md`.
