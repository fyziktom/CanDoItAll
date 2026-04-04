# Phase dependency map

| Phase | Goal | Findings | Dependencies | Gate |
| --- | --- | --- | --- | --- |
| Phase 0 | Install guardrails before more semantic change. | ACR-005, ACR-011, ACR-013 | None. This is the precondition layer. | Cycle rejection, node-scope integrity, and missing invariant tests must be in place before semantic refactors. |
| Phase 1 | Define semantics and ownership. | ACR-003, ACR-004, ACR-012 | Depends on Phase 0 guardrails so semantics can be changed safely. | Node kinds, relation semantics, and node-scoped actor truth must have one clear owner before graph reassembly. |
| Phase 2 | Rebuild read model and lifecycle around the clarified semantics. | ACR-001, ACR-006, ACR-014 | Depends on Phase 1 semantics/ownership decisions. | Assembled graph, projections, and note→typed transitions must work from the clarified canonical model. |
| Phase 3 | Decompose overloaded node and complete cross-module ownership seams. | ACR-002, ACR-007, ACR-008, ACR-015 | Depends on Phase 2 assembled graph + transition model. | Node carrier/facets, spatial semantics, artifact/storage separation, and actor ownership matrix are stabilized. |
| Phase 4 | Reduce orchestration and concurrency friction after semantics are stable. | ACR-009, ACR-010 | Depends on Phases 1-3 so decomposition is not performed over moving truth boundaries. | Service decomposition and lease narrowing happen only after truth boundaries stop moving. |
