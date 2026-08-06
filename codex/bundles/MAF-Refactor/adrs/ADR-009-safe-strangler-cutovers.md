# ADR-009: Use single-path strangler cutovers for runtime architecture migration

## Status

Accepted for this bundle.

## Decision

Every high-risk responsibility is migrated through expand, characterize, adapt, select one production path, observe, validate, and contract. Pure deterministic mapping may be compared in shadow. Side-effecting provider, tool, persistence, approval, process, and mutation paths are never executed twice.

## Consequences

- Temporary compatibility facades/selectors are permitted only with a named removal subbundle and telemetry.
- Workspace service migration is atomic per execution.
- Rollback disables or selects a complete old path for a new operation; it never mixes owners inside one run.
- SB17 is required before final deletion.
