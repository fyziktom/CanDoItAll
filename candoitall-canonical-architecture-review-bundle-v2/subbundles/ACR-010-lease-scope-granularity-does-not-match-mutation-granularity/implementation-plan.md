# Implementation plan

## Remediation goal

Define a mutation-to-scope policy matrix and progressively move safe node-level mutations to narrower scopes with telemetry and tests.

## Ordered steps

- Define a lease/lock matrix by mutation scope: project-wide, node-scoped, subtree-scoped, and bulk-transfer flows.
- Narrow single-node mutations to node-level or subtree-level leases where safe.
- Retain broader leases only for operations that truly rewrite large graph regions.
- Add concurrency-focused tests or simulations for common agent/user collisions.

## Guardrails

- Do not narrow lease scopes before invariants are centrally enforced.
- Prefer conservative correctness over optimistic concurrency in early phases.

## Acceptance criteria

- Mutation scope choice is explicit and test-backed.
- Node-level scopes are used only where the invariant model can support them safely.
