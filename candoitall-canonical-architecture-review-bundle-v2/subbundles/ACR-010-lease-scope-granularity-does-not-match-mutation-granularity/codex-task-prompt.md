# Codex task prompt — ACR-010

Implement finding `ACR-010` from this subbundle.

## Required stance

- follow the bundle architecture
- do not solve this by introducing a new parallel truth
- keep changes aligned with `Phase 4`
- preserve node-as-carrier and canonical spatial semantics where relevant
- add required positive and negative tests
- run the validation commands
- produce evidence for QA

## Finding summary

Scope kinds include ProjectNode, but many agent mutation flows still take project-wide leases, which may over-serialize work and obscure future permission boundaries.

## Ordered implementation steps

- Define a lease/lock matrix by mutation scope: project-wide, node-scoped, subtree-scoped, and bulk-transfer flows.
- Narrow single-node mutations to node-level or subtree-level leases where safe.
- Retain broader leases only for operations that truly rewrite large graph regions.
- Add concurrency-focused tests or simulations for common agent/user collisions.

## Guardrails

- Do not narrow lease scopes before invariants are centrally enforced.
- Prefer conservative correctness over optimistic concurrency in early phases.

## Done means

- Mutation scope choice is explicit and test-backed.
- Node-level scopes are used only where the invariant model can support them safely.
