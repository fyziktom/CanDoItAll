# Required implementation evidence

The closure package must include:
- single canonical containment model
- guardrail tests for hierarchy dual-write

## Mandatory proof
- Editable create/reparent/seed/move flows no longer persist hierarchy links; guardrail tests fail if Contains/BelongsTo is reintroduced for canonical editable nodes.
