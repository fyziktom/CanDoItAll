# Driver Completion Evidence Policy Extraction

## Status

- `Completed`

## Objective

- Extract managed artifact materialization, product completion rules, required tool receipt checks, required path checks, file content checks, and grounded reference validation into focused driver-owned policy services.

## Covered Inputs

- `R001` Preserve behavior.
- `R002` Split integration file.
- `R007` Extract completion evidence policies.
- `R010` Keep diagnostics explicit and actionable.
- `R013` Driver ports own completion evidence, prompt composition, and step execution dispatch behavior.
- `R014` Maintain one-way dependency direction from MAF/AgentFramework to Processes contracts.
- `N007` Completion evidence, runtime process dispatching, and prompt fragment composition must be driver-owned.
- `N008` Processes must not depend on the MAF wrapper.

## Prerequisites

- SB01 boundary and project placement gate passed.
- SB02 adapter decomposition gate passed or explicitly documents which adapter methods remain temporarily owned by SB04.
- SB01/SB02 dependency scans prove evidence policy placement does not require a `src/Processes/*` project to reference MAF or `CanDoItAll.Modules.AgentFramework`.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeLaunchVariables.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverPackage.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Tools/WorkspaceArtifactToolService.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandReceiptWriter.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeIntegrationAdapterTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFrameworkRuntimeToolReceiptTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkspaceArtifactToolServiceTests.cs`

## Deliverables

- Driver-owned managed artifact materializer service with direct file-service tests.
- Driver-owned product completion requirement parser for paths, tool receipts, and file content checks.
- Driver-owned product root/path validator that rejects outside-root paths and invalid aliases predictably.
- Driver-owned tool receipt matcher that validates current-run, step-relevant, product mutation, validation, runtime, browser, and project-structure receipts.
- Driver-owned grounded reference validator and sanitizer for outcome text and managed artifact bodies.
- Typed policy result records with actionable diagnostics and retry/idempotency classification exposed through SB01-approved driver ports.

## Dependency Impact

- SB07 depends on this phase to prove software-delivery/product-mutating processes remain protected against false completion.
- SB06 depends on this phase when step execution dispatch calls into driver completion-evidence validation instead of generic runtime private methods.
- SB05 depends on this phase only for shared launch-variable contract clarity; domain contributors may continue to emit the same variables until a typed replacement is proven.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof and artifact-backed proof manifest.

## Implementation Steps

1. Identify current private methods for managed artifact materialization, product completion required paths, required tool receipts, file content checks, product root inspection, external-target alias conversion, and grounding.
2. Extract cohesive driver-owned services and typed result records without changing existing launch variable names or diagnostic codes unless tests force correction.
3. Add negative tests for missing evidence refs, missing product files, outside-root paths, missing required receipts, stale/non-current receipts, invalid file-content checks, and ungrounded path-like refs.
4. Add positive tests for current-run product mutation proof, managed artifact self-evidence producer completion, accepted completed primary artifacts, valid file content checks, and grounded refs from launch variables/tool receipts.
5. Preserve integration adapter behavior by wiring extracted policies into the AgentFramework/MAF process driver orchestration.
6. Update proof artifacts and execution report.

## Scope Exceptions

- Replacing launch variables with a new typed policy model is allowed only if all existing template and launch contributor behavior remains compatible. Otherwise create typed wrappers around existing variables and defer full migration.

## Do Not Do

- Do not weaken completion evidence rules to make tests easier.
- Do not accept manual seeding of product completion proof as a positive feature test unless it is explicitly a parser/validator fixture.
- Do not allow native absolute paths, scoped storage paths, managed-files paths, project-media paths, or tool-runs paths as final evidence unless current-run grounding rules explicitly allow them.
- Do not move product mutation, tool receipt, managed artifact, or grounding policy into generic `CanDoItAll.Processes.Application` or `CanDoItAll.Processes.Runtime`.
- Do not add a MAF, AgentFramework, or `CanDoItAll.Modules.AgentFramework` reference to any `src/Processes/*` project to host evidence policy.

## Acceptance Checklist

- Evidence policies are independently testable.
- Adapter orchestration no longer owns large private policy parsers.
- Evidence policies are owned by the selected driver path, not by generic runtime/application dispatch.
- Product mutation completion still requires product evidence and current-run receipts when policy says so.
- Managed artifact materialization still writes/appends expected current-run refs.
- Grounding sanitizer removes or rejects non-citable refs and records actionable diagnostics.
- Static scans prove evidence extraction did not introduce Processes-to-MAF dependencies.

## Proof Required

- `proof/SB04/manifest.md` with changed-file hashes, command transcripts, source assertions, anti-stub audit output, and host/file proof when filesystem behavior changes.
- `proof/SB04/semantic-invariants.md` covering product proof, managed artifact materialization, required receipt enforcement, file content checks, and grounded reference rejection.
- Production Behavior Artifact Matrix if a new production policy record, signal, state, or event is introduced.
- Failing-first tests for false completion without required product proof and for ungrounded path refs.
- Dependency-direction scan transcript proving no Processes-to-MAF reference after evidence extraction.
- Passing policy and adapter regression test transcripts.

## Browser Validation Logging

- N/A for browser UI.
- Host/file validation must be logged in `reviews/01-execution-report.md` when product-root or path validation behavior changes.

## Progression Gate

- SB07 may rely on product completion proof only after SB04 proves false-completion negative cases and realistic product mutation positive cases through extracted driver-owned policy services and dependency scans show Processes still does not reference MAF.

## Suggested Agent Prompt

```text
Implement SB04 only. Extract completion evidence policy from the AgentFramework adapter into focused driver-owned services. Preserve launch-variable compatibility, diagnostic codes, and false-completion protections. Do not add any MAF or Modules.AgentFramework reference to src/Processes. Add negative and positive tests for product paths, receipts, file content checks, managed artifacts, and grounded references. Capture proof/SB04/manifest.md and proof/SB04/semantic-invariants.md before closure.
```

