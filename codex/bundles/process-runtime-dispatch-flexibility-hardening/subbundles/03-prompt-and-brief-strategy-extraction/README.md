# Prompt And Brief Driver Strategy Extraction

## Status

- `Completed`

## Objective

- Extract prompt and step brief composition into driver-owned typed strategies or contributors so generic process prompts stay domain-neutral and AgentFramework/MAF prompt behavior can be mocked, replaced, or varied per model/provider without editing process runtime/application dispatch code.

## Covered Inputs

- `R003` Keep runtime domain-neutral.
- `R005` Extract prompt and brief composition into replaceable strategies.
- `R012` Prove enterprise-domain flexibility.
- `R013` Driver ports own completion evidence, prompt composition, and step execution dispatch behavior.
- `R014` Maintain one-way dependency direction from MAF/AgentFramework to Processes contracts.
- `N003` Isolate prompt builders into drivers or strategies.
- `N007` Completion evidence, runtime process dispatching, and prompt fragment composition must be driver-owned.
- `N008` Processes must not depend on the MAF wrapper.

## Prerequisites

- SB01 boundary and project placement gate passed.
- SB01 must define where prompt composition driver contracts and AgentFramework/MAF prompt contributors live.
- SB01 dependency scan must prove prompt strategy placement does not require a `src/Processes/*` project to reference MAF or `CanDoItAll.Modules.AgentFramework`.

## Exact Source References

- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessStepBriefContracts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverPackage.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessStrategyDispatcher.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessLaunchPromptTests.cs`
- `repo://Templates/Processes`

## Deliverables

- A driver-owned prompt composition port or equivalent extension of existing driver contracts with typed request/result records.
- Current AgentFramework/MAF prompt content moved from monolithic builder methods into focused contributors owned by the AgentFramework/MAF process driver or an SB01-approved transitional module-owned shim.
- Generic brief builder remains domain-neutral and testable; it can provide fallback brief data but must not own provider/model prompt fragments.
- Model/provider variation seam documented and covered by a fake driver strategy test.
- DI registration for default generic and AgentFramework/MAF prompt composition through driver registration/composition.

## Dependency Impact

- SB05 depends on this phase because domain launch context must feed prompt strategies without leaking .NET/software-delivery assumptions into generic prompts.
- SB06 depends on this phase for the selected step execution driver to receive prompts from driver-owned composition rather than private runtime methods.
- SB07 depends on prompt strategy tests to prove runtime flexibility beyond app-building.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof and artifact-backed proof manifest.

## Implementation Steps

1. Define or confirm the prompt composition driver contract using typed request data, not ad hoc string flags.
2. Move AgentFramework/MAF-specific fragments into named contributors for execution contract, manager escalation, evidence citation, project-structure context, product mutation, own-output bootstrap, dependency artifacts, subprocess guidance, and evidence hygiene.
3. Preserve current prompt output where tests assert exact behavior.
4. Add tests proving generic prompts for business analysis, supplier analysis, reports preparation, quality management, and data analysis stay free of AgentFramework, .NET, Blazor, repository, screenshot, and project-structure launch guidance.
5. Add tests proving AgentFramework prompt strategy includes the current required governed execution and subprocess guidance.
6. Add a fake/model strategy test proving prompt composition can be replaced through driver registration without editing `ProcessLaunchApplicationService`, `ProcessRuntimeDispatchApplicationService`, or generic Processes projects.
7. Update proof artifacts and execution report.

## Scope Exceptions

- Do not redesign process template text in this phase; template domain text is handled in SB05 or SB07 if required.
- Do not introduce multiple real model-specific strategies until a real model/provider requirement exists.

## Do Not Do

- Do not keep long prompt fragments as private methods on a monolithic runtime integration class.
- Do not move AgentFramework finalizer/tool instructions into `GenericProcessStepBriefBuilder`, `ProcessLaunchApplicationService`, `ProcessRuntimeDispatchApplicationService`, or any `CanDoItAll.Processes.Application` private helper.
- Do not add a MAF, AgentFramework, or `CanDoItAll.Modules.AgentFramework` reference to any `src/Processes/*` project to host prompt fragments.
- Do not rely on raw string keys for strategy selection when a typed option or descriptor is available.

## Acceptance Checklist

- Prompt fragments are individually named and testable.
- Generic prompt tests prove non-software enterprise scenarios remain clean.
- AgentFramework prompt tests still prove governed execution, finalizer, evidence, subprocess, and product mutation guidance.
- `ProcessLaunchApplicationService` depends on a minimal prompt contract or driver abstraction, not concrete AgentFramework text.
- Prompt strategy replacement can be tested with a fake driver/composer.
- Static scans prove prompt extraction did not introduce Processes-to-MAF dependencies.

## Proof Required

- `proof/SB03/manifest.md` with changed-file hashes, command transcripts, source assertions, and anti-stub audit output.
- `proof/SB03/semantic-invariants.md` covering generic prompt neutrality, AgentFramework prompt preservation, and strategy replaceability.
- Failing-first proof for a generic enterprise prompt polluted by AgentFramework/.NET guidance or a strategy replacement that cannot be injected.
- Dependency-direction scan transcript proving no Processes-to-MAF reference after prompt extraction.
- Passing prompt unit test transcript.

## Browser Validation Logging

- N/A - no browser-visible behavior should change in SB03.

## Progression Gate

- SB05 may start only after generic and AgentFramework/MAF prompt strategies are separated through driver-owned composition, tests prove non-software prompts remain domain-neutral, and dependency scans show Processes still does not reference MAF.

## Suggested Agent Prompt

```text
Implement SB03 only. Extract prompt and brief composition into driver-owned typed strategies/contributors. Preserve current AgentFramework/MAF prompt behavior while proving generic prompts for non-software enterprise processes stay free of .NET, Blazor, repository, screenshot, project-structure, and AgentFramework finalizer guidance. Do not add any MAF or Modules.AgentFramework reference to src/Processes. Capture proof/SB03/manifest.md and proof/SB03/semantic-invariants.md before closure.
```

