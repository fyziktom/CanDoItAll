# Requirements

| ID | Requirement |
| --- | --- |
| RQ-001 | Review and preserve all previous runtime behavior; this is refactoring only. |
| RQ-002 | Do not create Process Core, production process driver APIs, driver registries, or driver packages. |
| RQ-003 | Keep browser/mobile/small/medium proof N/A unless UI files unexpectedly change; if UI changes, only large desktop proof is allowed. |
| RQ-004 | Create module-local helpers/coordinators for execution attempt flow without changing public contracts. |
| RQ-005 | Isolate response text resolution and active execution outcome creation. |
| RQ-006 | Isolate recovered/concurrent execution adoption while preserving polling, response, and chat-session behavior. |
| RQ-007 | Isolate execution request construction and failed execution normalization. |
| RQ-008 | Isolate post-attempt fact collection without changing completion status/reason calculation. |
| RQ-009 | Isolate retry decision families: incomplete successful run, failed run, provider failure, browser proof failure, and repairable blocked outcome. |
| RQ-010 | Isolate no-progress retry signal, fingerprint, mutation/proof deltas, and ledger compression. |
| RQ-011 | Isolate provider fallback candidate selection, health probing, assigned-agent update, and provider recovery directive assembly with explicit side-effect boundaries. |
| RQ-012 | Preserve recovery journal, rework packet, typed recovery directive, and next-attempt scheduling behavior. |
| RQ-013 | Update driver-readiness documentation only; do not implement drivers. |
| RQ-014 | Add focused test coverage and source assertions for each critical gate. |
| RQ-015 | Reduce `Execution.cs` and/or `Concurrency.cs` line counts materially without wrapper-only fake extraction. |
