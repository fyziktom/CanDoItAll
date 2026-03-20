# Prompt 08 — Docs And Validator Handoff

## Objective

Leave the repo in a state where a validator agent can audit it efficiently.

## Tasks

1. Update or add docs in `docs/pdmx-workstation`.
2. Document:
   - schema changes
   - grouping modes
   - threshold profile locations
   - copied-DB benchmark workflow
   - manual lock semantics
3. Produce a short validator-focused summary in-repo:
   - what changed
   - which risks remain
   - how to run validation safely

## Boundaries

- do not write generic docs disconnected from actual implementation
- keep docs aligned with real file names and routes

## Review checklist

- [ ] docs mention copy-first DB validation
- [ ] docs describe dry-run vs apply
- [ ] docs list manual override semantics
- [ ] docs mention how to interpret confidence bands
