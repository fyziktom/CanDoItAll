# Phase plan

| Phase | Goal | Findings | What closes | Gate |
| --- | --- | --- | --- | --- |
| Phase 0 | Install guardrails before more semantic change. | ACR-005, ACR-011, ACR-013 | Workbench reparent flow lacks explicit cycle and parent invariants; Core canonical-invariant and projection-equivalence tests are missing; Node-scoped CRM/HR assignments use a soft NodeKey reference without canonical integrity checks | Cycle rejection, node-scope integrity, and missing invariant tests must be in place before semantic refactors. |
| Phase 1 | Define semantics and ownership. | ACR-003, ACR-004, ACR-012 | Type and subtype semantics are weak and partly owned by the UI catalog; Relation semantics are blurred and hierarchy is stored twice; Party responsibility truth is duplicated across node metadata, assignment tables, and module-local fields | Node kinds, relation semantics, and node-scoped actor truth must have one clear owner before graph reassembly. |
| Phase 2 | Rebuild read model and lifecycle around the clarified semantics. | ACR-001, ACR-006, ACR-014 | Persisted system-managed workbench graph acts as a parallel truth; Calendar and Gantt are projections over a persisted projection; Node reclassification and typed lifecycle history are insufficient for note→task/decision evolution | Assembled graph, projections, and note→typed transitions must work from the clarified canonical model. |
| Phase 3 | Decompose overloaded node and complete cross-module ownership seams. | ACR-002, ACR-007, ACR-008, ACR-015 | ProjectObjectRecord is an overloaded universal box; Route, artifact binding, and storage/media concerns leak into node truth; Spatial semantics are canonical, but marker ownership is duplicated and under-modeled; Cross-module responsibility model is fragmented and lacks one canonical actor-assignment owner | Node carrier/facets, spatial semantics, artifact/storage separation, and actor ownership matrix are stabilized. |
| Phase 4 | Reduce orchestration and concurrency friction after semantics are stable. | ACR-009, ACR-010 | ProjectWorkbenchService is an oversized orchestration hotspot; Lease scope granularity does not match mutation granularity | Service decomposition and lease narrowing happen only after truth boundaries stop moving. |

## Execution order rationale

- **Phase 0 first** because unsafe mutations and missing guardrails make everything else risky.
- **Phase 1 second** because semantics and ownership have to be explicit before graph assembly or lifecycle work can stabilize.
- **Phase 2 third** because read-model rebuild and lifecycle history depend on clarified semantics.
- **Phase 3 fourth** because node decomposition should follow the semantic decisions, not precede them blindly.
- **Phase 4 last** because service extraction and lease tuning are safer after the truth boundaries stop moving.
