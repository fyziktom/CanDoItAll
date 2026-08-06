# ADR-011: Diagnose refactor regressions by correlated owner stage

## Status

Accepted for SB17 and release operation.

## Decision

Persist and log bounded correlation identities across admission, context, authority, scope, runtime adapter, provider, tool, approval, persistence, process, workflow, and UI refresh stages. Classify the first failed invariant and fix the canonical owner with a failing regression test.

## Consequences

- Cross-boundary symptoms are not patched at arbitrary callers.
- Sensitive prompts, attachments, secrets, and raw tool arguments are excluded.
- Bug records and session handoffs become required proof artifacts.
