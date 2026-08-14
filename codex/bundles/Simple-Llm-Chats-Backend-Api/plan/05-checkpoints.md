# Architecture checkpoints

## CP0 — baseline and decisions

Pass only when:

- current owners and project references are re-inventoried;
- canonical organization/subject/provider/error conventions are resolved;
- baseline focused tests are recorded;
- no production code changed;
- SB01 file plan is concrete.

## CP1 — backend architecture

Review the complete feature block after SB06.

Pass only when:

- two new projects have the prepared dependency direction;
- generic transcript behavior remains backward compatible;
- production uses EF store and named product engine;
- definitions pin immutable revisions;
- cross-process CAS is proven;
- profile switch blocks dispatch and commit;
- database-transfer registration and referential round-trip are proven;
- idempotency and crash reconciliation are proven;
- no UI/agent/tool/process dependency exists;
- provider-profile/capability contracts and invocation-port registration have provider-runtime ownership, not Core/Workflow ownership for the new path;
- provider/model thinking-effort availability, typed override, dispatch, fingerprint, and audit reuse one canonical capability policy and distinguish provider default from explicit `None`;
- no dormant feature table or generic manager has been added.

## CP2 — API architecture

Pass only when:

- route families are separated from agents;
- DTOs do not expose EF/generic internal documents/provider credentials;
- expected revision and idempotency are mandatory where required;
- errors are stable and sanitized;
- authorization follows repository conventions;
- focused real-host PostgreSQL tests pass;
- canonical database-transfer round-trip remains valid;
- OpenAPI is accurate;
- provider-options and definition schemas describe the nullable thinking-effort contract and per-model allowed values without exposing provider configuration;
- no UI was added.

## FINAL

Pass only after SB11 executes the one stable solution gate and all residual items are explicitly
classified.
