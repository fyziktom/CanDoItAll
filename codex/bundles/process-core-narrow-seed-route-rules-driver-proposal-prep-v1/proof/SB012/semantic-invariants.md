# SB012 Semantic Invariants

- Invariant ID: `SB012-MODULE-ADAPTER-PARITY`
- Source raw note: Keep Core pure and preserve dispatch behavior.
- Expected behavior: The module maps its candidate/entity model into Core snapshots without changing route execution semantics.
- Disallowed shallow implementation: Passing EF entities into Core, making Core reference the module, or bypassing existing dispatch route handlers.
- Failing-first test: N/A process/no production behavior; adapter drift is covered by focused integration tests and dependency scans.
- Passing test: bundle://proof/common/transcripts/integration-dispatch.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs and repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteExecutionModels.cs
- Production assertions: The dispatch service still owns orchestration, claims, transitions, finalizer application, storage, and AgentFramework execution.
- Red-team negative case: bundle://proof/common/transcripts/core-forbidden-scan.txt rejects module/entity dependency leakage into Core.
- Downstream dependency check: bundle://proof/common/transcripts/build-solution.txt proves consumers compile after enum and route-type movement.
