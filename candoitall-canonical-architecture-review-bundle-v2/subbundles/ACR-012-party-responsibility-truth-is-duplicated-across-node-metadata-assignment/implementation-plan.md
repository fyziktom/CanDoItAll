# Implementation plan

## Remediation goal

Choose one canonical owner for cross-cutting actor responsibility. Recommended direction: canonical ActorAssignment model with explicit scopes; node metadata keeps only display/cache data if needed, and module-local fields become derived mirrors or migrate later.

## Ordered steps

- Choose one authoritative owner for node-scoped party/actor truth and stop writing the same relationship into both metadata and assignment rows.
- For participant/meeting/work-item nodes, move live party membership/assignee truth to the canonical scoped actor-assignment owner (or explicit node-to-actor link) and project names back into UI.
- Keep metadata only for typed node details that are intrinsic to the node, not for duplicated live directory ownership.
- Add migration/backfill logic that reads old metadata, writes canonical assignments/links, and then clears or demotes duplicated metadata fields.

## Guardrails

- Do not leave node metadata IDs and assignment rows independently editable.
- Do not introduce a hidden second mirror while removing the first one.

## Acceptance criteria

- Node-scoped responsibility has one canonical writable owner.
- Module-local responsible-party fields have an explicit ownership rule (derived mirror or authoritative until migrated).
- No UI flow writes both metadata IDs and assignment rows as independent truths.
