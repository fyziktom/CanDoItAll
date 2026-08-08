# Secrets and key bootstrap

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
