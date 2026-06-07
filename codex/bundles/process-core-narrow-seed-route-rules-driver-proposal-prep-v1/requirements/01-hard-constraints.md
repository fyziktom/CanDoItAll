# Hard Constraints

REQ-001: Create no broad Process Core extraction.

REQ-002: If `CanDoItAll.Processes.Core` is created, it must contain only pure route-stage/read-model/rule code in this bundle.

REQ-003: `CanDoItAll.Processes.Core` must not reference:

- `CanDoItAll.Modules.Processes`
- `CanDoItAll.Infrastructure`
- `CanDoItAll.AgentFramework.*`
- `Microsoft.EntityFrameworkCore`
- workspace/storage/filesystem services
- UI, plugin, MAF, driver, or manager projects

REQ-004: Process module may reference Core only from module-local adapter/rule call sites.

REQ-005: Existing functionality must be preserved.

REQ-006: Route stage order must remain exactly:

`FreshRecoverySkip -> DatabaseRequirement -> UpstreamMaterialization -> StrandedArtifactRecovery -> Subprocess -> StartTransition -> Workflow -> DirectAgentExecution -> CompetingExecutionGuard -> RunClosedGuard -> FinalizerTransition`

REQ-007: No production process-driver API, registry, DI registration, runtime selector, manager tool, or helper-driver execution path may be created.

REQ-008: No UI/browser/mobile/small/medium proof. Runtime/service-only changes must keep browser validation N/A.

REQ-009: Execution report must keep individual SB rows; no collapsed `SB001-SB030` row as the only proof.

REQ-010: Any adapter compatibility wrappers must remain module-local and must have source scans proving they do not leak into Core.
