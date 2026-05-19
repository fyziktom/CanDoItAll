# Assumptions And Risks

## Working Assumptions

- Docker Desktop and the existing `docker-compose.yml` services are available on the local machine.
- Qdrant gRPC is reachable on `localhost:6334`.
- PostgreSQL is reachable on `localhost:5432` with the compose credentials.
- The beta gate should be proven through the CanDoItAll app/API and browser where possible.
- Durable memory truth remains in `AppDbContext`; Qdrant is only projection proof.

## Critical Path Risks

- Live Qdrant projection may fail because the app lacks a seeded projection row, profile, embedding configuration, or payload-index compatibility.
- Existing PostgreSQL data may be dirty; validation must either use deterministic scoped data or clearly record the active profile state.
- A running web process may lock build outputs; validation must stop/restart the local app when builds are required.
- P0 coverage may still be insufficient if projection rebuild or automation is not observable in the real app flow.

## Validation Risks

- A rebuild response with only skipped items is not beta proof.
- A recall response that only records vector-unavailable warnings is not vector-provider proof.
- Direct Qdrant inspection can support projection proof but cannot replace durable app/API proof.
- Browser screenshots prove operator visibility, not semantic memory quality.

## Reopen Triggers

- Reopen P0/P1 implementation if projection rebuild cannot create or update Qdrant points from durable memory.
- Reopen docs if the true stage remains beta-candidate alpha after validation.
- Reopen API validation if v1 endpoints differ from the documented contract.
- Reopen operator UI proof if projection failure/success is not visible after live validation.

