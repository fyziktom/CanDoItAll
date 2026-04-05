# Skill back-check after proposed remediation

| Finding | Title | Prospective back-check expectation | Closure condition |
| --- | --- | --- | --- |
| ACR-005 | Workbench reparent flow lacks explicit cycle and parent invariants | Prospective pass if every mutation entry point routes through the invariant service and tests cover cycle cases. | Closes if acceptance + validation tests pass |
| ACR-011 | Core canonical-invariant and projection-equivalence tests are missing | Prospective pass if each remediation phase lands with new guardrail tests before broad refactors. | Closes if acceptance + validation tests pass |
| ACR-013 | Node-scoped CRM/HR assignments use a soft NodeKey reference without canonical integrity checks | Prospective pass if node-scoped assignments cannot be saved unless the target node exists in the same project and the role is allowed for that node kind. | Closes if acceptance + validation tests pass |
| ACR-012 | Party responsibility truth is duplicated across node metadata, assignment tables, and module-local fields | Prospective pass if participant/meeting/work-item party edits change one canonical assignment owner and any metadata/module-local display fields are derived or mirrored one-way only. | Closes if acceptance + validation tests pass |
| ACR-003 | Type and subtype semantics are weak and partly owned by the UI catalog | Prospective pass if all type/subtype decisions flow through registry lookups and the UI becomes a consumer, not the semantic owner, of node kinds. | Closes if acceptance + validation tests pass |
| ACR-004 | Relation semantics are blurred and hierarchy is stored twice | Prospective pass if hierarchy, dependency, and association edges have separate canonical meanings and storage owners. | Closes if acceptance + validation tests pass |
| ACR-001 | Persisted system-managed workbench graph acts as a parallel truth | Prospective pass if structure/calendar/Gantt consume CanonicalGraphAssembler output instead of persisted synced rows and actor overlays are attached during graph assembly, not by mutating the synced copy. | Closes if acceptance + validation tests pass |
| ACR-006 | Calendar and Gantt are projections over a persisted projection | Prospective pass if builders read from the same assembled graph and no projection becomes a write model. | Closes if acceptance + validation tests pass |
| ACR-014 | Node reclassification and typed lifecycle history are insufficient for note→task/decision evolution | Prospective pass if a brainstorm node can evolve into a task/decision/block without losing node identity, rationale history, or semantically meaningful position/marker context. | Closes if acceptance + validation tests pass |
| ACR-002 | ProjectObjectRecord is an overloaded universal box | Prospective pass if a stable NodeCarrier remains, semantically meaningful X/Y and marker sets stay canonical, and extension/facet data moves out of the universal box. | Closes if acceptance + validation tests pass |
| ACR-008 | Spatial semantics are canonical, but marker ownership is duplicated and under-modeled | Prospective pass if semantic X/Y and markers remain canonical, but only one writable marker owner exists and ephemeral canvas state stays in view-state records. | Closes if acceptance + validation tests pass |
| ACR-015 | Cross-module responsibility model is fragmented and lacks one canonical actor-assignment owner | Prospective pass if every responsibility fact can answer one question clearly: who owns it canonically, who only mirrors it, and who only projects it? | Closes if acceptance + validation tests pass |
| ACR-007 | Route, artifact binding, and storage/media concerns leak into node truth | Prospective pass if route becomes derived and attachment bindings are explicitly typed. | Closes if acceptance + validation tests pass |
| ACR-009 | ProjectWorkbenchService is an oversized orchestration hotspot | Prospective pass if the service reduces to orchestration over smaller collaborators and no collaborator mixes unrelated roles. | Closes if acceptance + validation tests pass |
| ACR-010 | Lease scope granularity does not match mutation granularity | Prospective pass if scope selection aligns with mutation semantics and remains conservative where invariants require broader locks. | Closes if acceptance + validation tests pass |

## Back-check judgment

Using the same skill lenses prospectively, the proposed remediation is directionally sound **only if**:

- node remains canonical for workbench-authored thinking
- actor truth gets one owner per scope
- projections consume one assembled graph
- transition history becomes explicit
- invariant tests are added before the next wave

If implementation drifts from those points, the skill back-check would fail even if local tests were green.
