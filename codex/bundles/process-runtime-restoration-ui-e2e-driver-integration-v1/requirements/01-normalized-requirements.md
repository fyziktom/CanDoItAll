# Normalized Requirements

| ID | Requirement |
| --- | --- |
| REQ-001 | Remove bundle-path dependencies from tests and source guardrails; no test may require `codex/bundles/<bundle-name>` to exist. |
| REQ-002 | Keep all useful architecture guard semantics by moving durable invariants to source-backed tests, stable docs, or test fixtures outside transient bundles. |
| REQ-003 | Prove solution build and full unit suite after removing bundle-path coupling. |
| REQ-004 | Prove web application starts on the current branch with current composition/DI. |
| REQ-005 | Inventory process UI routes, process template catalog, project/project-structure entry points, and process-run API/services. |
| REQ-006 | Restore/verify UI flow: large-screen user can choose a process/template from project context and start a process run. |
| REQ-007 | Restore/verify process run creation persistence and dispatch eligibility. |
| REQ-008 | Restore/verify dispatch claim/heartbeat/route/finalizer path using a deterministic/fake-agent runtime where possible. |
| REQ-009 | Restore/verify `.NET app create/modify` process scenario end-to-end enough to produce expected artifacts/evidence. |
| REQ-010 | Restore/verify generic business-analysis process scenario end-to-end without software-development domain leakage in generic core. |
| REQ-011 | Ensure read-only driver verification integration can help process manager/QA without introducing mutation side effects. |
| REQ-012 | Add process-level observation of verification results as diagnostics/evidence only, not as transition/finalizer/claim mutation. |
| REQ-013 | Keep Process Core deterministic and dependency-clean. |
| REQ-014 | Keep runtime host/registry/selector/DI/manager/scheduler/workflow hooks blocked unless explicitly approved in future phases. |
| REQ-015 | Large-screen Playwright proof only; no small/medium/mobile screenshots in this bundle. |
| REQ-016 | Create a stable release-candidate matrix for process runtime, UI launch, dispatch, scenarios, Core/driver boundaries, and docs. |