# Tasks

- [ ] Replace permissive boolean/integer defaulting with strict parsing; malformed configured values fail validation.
- [ ] Cap port mappings, environment variables, labels, mounts, total argument count, and total argument bytes.
- [ ] Validate `logs --since` against a bounded accepted duration/RFC3339 grammar and reject option-like values.
- [ ] Preserve endpoint allowlist, immutable image evidence, preflight, no-shell invocation, and bounded output.
- [ ] Harden database password-file loading with a small maximum size, non-empty/no-NUL validation, and safe file-type handling without logging content.
- [ ] Document PostgreSQL secret rotation behavior for an existing data volume; changing the file alone does not rotate an existing role password.
- [ ] Update future `containers` workflow logic to create and remove a disposable `.secrets/db-password` before Compose validation/start.
- [ ] Extend Docker validation to assert requirements per service, not merely somewhere in the file.
- [ ] Run clean-checkout local Compose app+db smoke; keep loopback-only alpha boundary.
