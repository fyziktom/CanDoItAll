# Assumptions And Risks

## Working Assumptions

- P1 can be solved as beta-hardening work without breaking the P0 API and UI entry points.
- Live Qdrant/provider execution may not be available locally, so deterministic adapter failure tests plus an executable runbook are acceptable proof for this workstation.
- Retention cleanup should start as an explicit operator/API action, not a hidden hosted background worker.
- API contract versioning can preserve legacy routes while documenting the stable v1 contract.

## Critical Path Risks

- Duplicating Minimal API route groups can collide on endpoint names if implemented carelessly.
- Retention deletion can violate FK relationships or accidentally remove canonical memory; the first P1 cleanup pass must target operational records with explicit dry-run and cutoff semantics.
- Sensitive-source policy can create false positives; the first pass should flag/reject high-risk patterns explicitly rather than silently ingesting or silently redacting.
- UI audit expansion can overload the existing page if it is not exposed through focused child component inputs.

## Validation Risks

- Browser proof may require the local app/database profile gate to be handled before the `/cognitive-memory` page renders.
- Provider failure behavior must be asserted through observable projection/run status, not swallowed exceptions.
- Retention tests need enough seeded graph data to prove counts and deletion order.

## Reopen Triggers

- Any v1 API route or contract endpoint breaks an existing unversioned route.
- Cleanup removes canonical memory records, source manifests, source items, or evidence anchors without an explicit request to do so.
- External source ingestion records sensitive-looking content as safe context after P1 hardening.
- Docs claim beta readiness without passing the release gate in the roadmap.
