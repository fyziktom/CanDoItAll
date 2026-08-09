# Secrets and key bootstrap

## A04 implementation decision

A04 implements explicit provider profiles and makes provider capability a startup contract:

- Windows Auto selects DPAPI.
- Non-Windows Auto selects `LocalUserFile`, a guaranteed basic local profile that does not require an interactive or external vault. It preserves the legacy AES-256-GCM file format, enforces `0700` on vault directories and `0600` on vault files, and truthfully reports `BasicLocal` because its local key is accessible to that same account.
- macOS Keychain and Linux Secret Service remain explicit stronger profiles. They require their real platform/session services and fail closed when unavailable; neither silently falls back after selection.
- Headless/service deployments can explicitly select the external wrapping-key vault. Its AES-256-GCM key is supplied by a protected environment source, is never written to the vault directory, and supports current plus retained previous key identifiers for rotation.
- The legacy `DataProtectionFile` provider name and in-memory vault remain rejected outside Development and require explicit insecure-development opt-in. Authorized Development use reports `DevelopmentOnly`; migration opens the raw legacy implementation only through the read/delete boundary.

Every selected vault implements a capability probe. A hosted startup validator publishes provider kind, availability, protection level, and non-secret remediation/security notice. It prevents the application from serving when the configured provider is unsupported, unavailable, locked, dependency-missing, or incompatible with the current session/profile, and emits a structured warning for every available non-`Strong` profile.

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

- Auto selects current-user DPAPI and reports `Strong`; `LocalUserFile` is rejected because Windows already has this built-in stronger baseline.
- Migration to another provider is explicit; do not break old DPAPI payloads.
- Data Protection ring may use DPAPI or another configured protector.

### macOS interactive

- `LocalUserFile` is the no-dependency Auto baseline; select Keychain for stronger OS-vault isolation.
- Define whether access requires an unlocked user session.
- Headless/launchd must not pretend this profile is available.

### Linux interactive

- `LocalUserFile` is the no-dependency Auto baseline; select Secret Service for stronger session-vault isolation.
- Explicit Secret Service selection retains D-Bus/session/locked-state semantics and does not silently fall back when unavailable.

### Headless/service

The Unix basic local provider works without an interactive session. Higher-security services should explicitly use a provider independent of an interactive keyring, for example:

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

The local AES key beside ciphertext provides encrypted-at-rest handling against casual file disclosure, not isolation from code running as the same OS account. The Unix-only `LocalUserFile` profile keeps this behavior as an explicit basic protection tier with enforced `0700` directory and `0600` file modes, a typed `BasicLocal` capability, and a startup warning. It must never be described as equivalent to DPAPI, Keychain, Secret Service, or an externally protected wrapping key.

The `DataProtectionFile` name is retained only for Development compatibility and migration. New writers use `LocalUserFile`; existing payloads remain readable without a bulk rewrite because both names share the exact file format.

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

A04's schema-3 scanner reports only metadata and fingerprints for reviewed synthetic secret-scanner/redaction fixtures embedded in TRX parameterized test names. It accepts private sentinel-file inputs without echoing or scanning them and explicitly accounts for scanned, oversized, non-text, unreadable, and control-input files. The SEC-014 scan loaded two private sentinels, found zero sentinel matches, and skipped no source evidence. See `artifacts/unix-portability/A04/remediation-2/A04-remediation-2-secret-scan-classification.md`.
