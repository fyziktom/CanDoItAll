# Tasks

- [x] Replace permissive boolean/integer defaulting with strict parsing; malformed configured values fail validation.
- [x] Cap port mappings, environment variables, labels, mounts, total argument count, and total argument bytes.
- [x] Validate `logs --since` against a bounded accepted duration/RFC3339 grammar and reject option-like values.
- [x] Preserve endpoint allowlist, immutable image evidence, preflight, no-shell invocation, and bounded output.
- [x] Harden database password-file loading with a small maximum size, non-empty/no-NUL validation, and safe file-type handling without logging content.
- [x] Document PostgreSQL secret rotation behavior for an existing data volume; changing the file alone does not rotate an existing role password.
- [x] Update future `containers` workflow logic to create and remove a disposable `.secrets/db-password` before Compose validation/start.
- [x] Extend Docker validation to assert requirements per service, not merely somewhere in the file.
- [x] Run clean-checkout local Compose app+db smoke; keep loopback-only alpha boundary.
