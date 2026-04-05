# Codex task prompt — ACR-012

Implement finding `ACR-012` from this subbundle.

## Required stance

- follow the bundle architecture
- do not solve this by introducing a new parallel truth
- keep changes aligned with `Phase 1`
- preserve node-as-carrier and canonical spatial semantics where relevant
- add required positive and negative tests
- run the validation commands
- produce evidence for QA

## Finding summary

CRM/HR responsibility is now stored in more than one editable place. Participant, meeting, and work-item flows write both node metadata and project-party assignments, while Resources, Validation, and TestLab also store module-local responsible-party fields.

## Ordered implementation steps

- Choose one authoritative owner for node-scoped party/actor truth and stop writing the same relationship into both metadata and assignment rows.
- For participant/meeting/work-item nodes, move live party membership/assignee truth to the canonical scoped actor-assignment owner (or explicit node-to-actor link) and project names back into UI.
- Keep metadata only for typed node details that are intrinsic to the node, not for duplicated live directory ownership.
- Add migration/backfill logic that reads old metadata, writes canonical assignments/links, and then clears or demotes duplicated metadata fields.

## Guardrails

- Do not leave node metadata IDs and assignment rows independently editable.
- Do not introduce a hidden second mirror while removing the first one.

## Done means

- Node-scoped responsibility has one canonical writable owner.
- Module-local responsible-party fields have an explicit ownership rule (derived mirror or authoritative until migrated).
- No UI flow writes both metadata IDs and assignment rows as independent truths.
