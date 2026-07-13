# Runtime Driver Boundary And Inventory

## Status

- `Completed`

## Objective

- Establish binding architecture decisions before extraction starts: driver-owned ports for prompt composition, completion evidence, and step execution dispatch; project placement; dependency direction; responsibility inventory; file-size targets; and proof plan.

## Covered Inputs

- `R001` Preserve all functionality.
- `R002` Split `ProcessRuntimeIntegrationServices.cs` and adjacent runtime hotspots.
- `R003` Keep generic process runtime and application concepts domain-neutral.
- `R004` Isolate AgentFramework execution behavior behind process driver services.
- `R009` Clean dispatcher branch and recovery responsibilities.
- `R013` Driver ports own completion evidence, prompt composition, and step execution dispatch behavior.
- `R014` Maintain one-way dependency direction from MAF/AgentFramework to Processes contracts.
- `R015` Keep generic runtime dispatch orchestration separate from driver-owned step execution dispatch.
- `N006` Isolate domain-specific parts in own drivers, contributors, or projects.
- `N007` Completion evidence, runtime process dispatching, and prompt fragment composition must be driver-owned.
- `N008` Processes must not depend on the MAF wrapper.

## Prerequisites

- Bundle readiness gate must pass.
- No production code edits from later subbundles may start before this phase records the final boundary decision.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterDescriptors.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard/CanDoItAll.Processes.Drivers.Standard.csproj`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessStrategyDispatcher.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverPackage.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverDescriptor.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`
- `repo://src/Processes/CanDoItAll.Processes.Application/CanDoItAll.Processes.Application.csproj`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/CanDoItAll.Processes.Runtime.csproj`
- `repo://CanDoItAll.slnx`

## Deliverables

- Final decision on the AgentFramework/MAF process driver placement. Preferred result: MAF-owned driver implementation references Processes driver abstractions from below; Processes projects do not reference MAF.
- Driver port design for step execution dispatch, prompt composition, completion-evidence validation, driver recovery policy, and driver telemetry/observation mapping.
- Responsibility inventory mapping every class currently in `ProcessRuntimeIntegrationServices.cs` to its target service/file.
- Dependency direction notes for Application, Runtime, Drivers.Abstractions, Drivers.Standard, Modules.Processes, Modules.Workbench, and AgentFramework projects.
- File-size and coupling targets for the rest of the bundle.
- Direct test plan per extracted service.

## Dependency Impact

- SB02-SB06 depend on this phase. If the driver ports or dependency boundary are wrong, every extracted service may need to move again.
- SB07 depends on the inventory to audit that no responsibility was lost or hidden in a new monolith.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof because this phase defines the architecture invariants all later subbundles must follow.

## Implementation Steps

1. Read the source references and record actual project references, dependency direction, and current class ownership.
2. Decide the AgentFramework/MAF driver implementation placement by checking whether an MAF-owned driver project can reference Processes driver abstractions without cycles.
3. Define driver-owned ports for step execution dispatch, prompt composition, completion evidence, driver recovery, and telemetry/observation mapping.
4. Define target namespaces/folders for driver catalog, executor resolution, prompt strategies, adapter orchestration, subprocess lifecycle, evidence policies, observation/telemetry, and recovery services.
5. Define allowed public/internal contracts and decide which seams belong in `Drivers.Abstractions`, `Application`, module internals, Workbench, or MAF-owned driver projects.
6. Record migration-safe file-size and coupling targets, including a target for reducing `ProcessRuntimeIntegrationServices.cs` to composition-only or deleting it.
7. Add or update tests only if necessary to lock the boundary decision before extraction.
8. Update the execution report and proof manifest.

## Scope Exceptions

- This phase does not extract production behavior beyond boundary-enabling moves if required for compilation.
- This phase does not change process runtime semantics.

## Do Not Do

- Do not move AgentFramework-specific code into generic runtime/application projects.
- Do not create a `src/Processes/*AgentFramework*` driver project that references MAF assemblies.
- Do not create broad interfaces with one trivial implementation unless they are required for driver replacement or testing.
- Do not start prompt, adapter, evidence, launch contributor, or dispatcher extraction before this gate passes.

## Acceptance Checklist

- The final boundary decision is documented with project reference evidence.
- Driver-owned step execution dispatch, prompt composition, completion evidence, recovery, and telemetry ports are defined or explicitly mapped to existing extensible contracts.
- Every class in `ProcessRuntimeIntegrationServices.cs` has a target owner.
- Generic runtime and application allowed dependencies are documented.
- Static scans prove no `src/Processes/*` project references MAF, AgentFramework, or `Modules.AgentFramework`.
- Later subbundle prerequisites reference concrete SB01 artifacts.
- File-size/coupling targets are specific enough for SB07 to audit.

## Proof Required

- `proof/SB01/manifest.md` with changed-file hashes, command transcripts, source assertions, and anti-stub audit output.
- `proof/SB01/semantic-invariants.md` covering domain-neutral runtime boundary, driver placement, and no hidden AgentFramework dependency in generic runtime.
- Transcript for dependency graph/source inventory command.
- Transcript for static dependency scans from `inventories/02-dependency-direction-inventory.md`.
- Transcript for targeted build or compile check if any project files or contracts change.
- Failing-first proof is required if a boundary test is added to catch an existing dependency leak.

## Browser Validation Logging

- N/A - no browser-visible behavior should change in SB01.

## Progression Gate

- SB02-SB06 may start only after SB01 records the final driver-port boundary map and the proof manifest shows the chosen MAF/AgentFramework driver placement can compile without any Processes-to-MAF reference.

## Suggested Agent Prompt

```text
Implement SB01 only. Validate the project/reference boundary before moving behavior. Define driver-owned ports for step execution dispatch, prompt composition, completion evidence, driver recovery, and telemetry. Produce a responsibility inventory for ProcessRuntimeIntegrationServices.cs, decide MAF/AgentFramework driver placement below Processes, set file-size/coupling targets, capture proof/SB01/manifest.md and proof/SB01/semantic-invariants.md, then update the execution report. Stop if dependency direction would force any src/Processes project to depend on Workbench, ProjectStructure, AgentFramework/MAF, Modules.AgentFramework, .NET templates, or UI screenshot concepts.
```

