# Codex task prompt — ACR-014

Implement finding `ACR-014` from this subbundle.

## Required stance

- follow the bundle architecture
- do not solve this by introducing a new parallel truth
- keep changes aligned with `Phase 2`
- preserve node-as-carrier and canonical spatial semantics where relevant
- add required positive and negative tests
- run the validation commands
- produce evidence for QA

## Finding summary

The product workflow starts with fast brainstorming notes that later become structured tasks, decisions, or other typed nodes. Current reclassification mutates the same row in place, only supports note→block / block→block, and does not preserve typed transition history.

## Ordered implementation steps

- Model note→task/decision/etc. as transitions on a stable workbench-native node identity.
- Add `NodeTransitionHistory` (or equivalent) that records from-kind, to-kind, timestamp, actor, and optional reason/snapshot.
- Represent typed behavior through facets so transitions create/retire facets rather than destructively replacing the node.
- Define transition policies for what persists automatically: spatial semantics, explicit edges, attached artifacts, schedule, and actor assignments.

## Guardrails

- Do not solve this by deleting/recreating nodes and losing identity by default.
- Do not lose semantic X/Y, markers, or actor assignments during type evolution unless policy explicitly says so.

## Done means

- A brainstorm node can evolve into a richer typed node while keeping a stable identity and transition history.
- The transition matrix is registry-owned and test-backed.
- Assignments and spatial semantics are handled explicitly during transition.
