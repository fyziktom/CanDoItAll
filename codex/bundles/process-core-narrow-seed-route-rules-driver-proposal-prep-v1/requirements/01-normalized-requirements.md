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


# Acceptance Criteria

AC-001: `CanDoItAll.Processes.Core` exists only if the bundle moves the narrow route pure-rule family.

AC-002: Core project builds independently with allowed dependencies only.

AC-003: Architecture tests fail if Core references forbidden assemblies/namespaces.

AC-004: Existing route stage order and eligibility behavior is preserved by unit tests.

AC-005: Process module route pipeline keeps the same order and behavior.

AC-006: No route handler, route service, claim lifecycle, transition execution, finalizer application, AgentFramework execution, EF query, storage/file IO, or workspace behavior enters Core.

AC-007: Driver readiness remains proposal-only; production source scan rejects driver tokens.

AC-008: Full solution build passes.

AC-009: Full unit tests pass, or any unrelated failures are separately documented with proof they are unrelated.

AC-010: Focused dispatch/route integration tests pass.

AC-011: Source scans prove no UI/media drift.

AC-012: Final red-team explicitly decides whether a second Core family can be proposed next.
