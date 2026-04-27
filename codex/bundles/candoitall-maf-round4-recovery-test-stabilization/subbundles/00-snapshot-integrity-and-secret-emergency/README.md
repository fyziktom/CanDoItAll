# 00 — Snapshot Integrity and Secret Emergency


## Problem

The attached snapshot contradicts the latest Codex report. A real-looking provider key remains in `src/CanDoItAll.Web/appsettings.json`, and claimed secret-scanning/recovery-model files are missing.

## Tasks

1. Remove the provider key from `src/CanDoItAll.Web/appsettings.json`. Replace it with no value or a clear placeholder that cannot match provider-key regexes.
2. Add documentation instructing developers to configure provider keys through environment variables, user secrets, or a secure secret provider.
3. Add `SecretScanningTests` or an equivalent repository secret scan script. It must scan source-controlled text files, exclude build artifacts, and allow only explicit placeholders.
4. Add a snapshot integrity test/script that verifies required round deliverables exist after implementation.
5. Add a clear note that the already-exposed key must be revoked/rotated outside the repository.

## Acceptance criteria

- No real-looking provider key remains in appsettings or docs.
- Secret scan fails if a realistic OpenAI/GitHub/Azure key is committed.
- Secret scan passes with intentional placeholder fixtures only.
- The execution report does not contain raw secrets.
- Codex must list the exact secret-scanning file and command that executed it.

## Suggested tests

- `SecretScanningTests.Repository_contains_no_real_provider_keys`
- `SecretScanningTests.Placeholder_allowlist_does_not_allow_realistic_project_keys`
- `SnapshotIntegrityTests.Round4_required_artifacts_exist`

