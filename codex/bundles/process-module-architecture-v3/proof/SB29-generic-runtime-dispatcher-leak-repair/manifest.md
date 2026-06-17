# SB29 Proof Manifest

## Summary

SB29 removed current implementation leaks from the generic Process launch/runtime-dispatch path by extracting step brief construction behind an application port, moving AgentFramework/project-structure prompt details into the module adapter layer, renaming the generic adapter kind from `ProjectContext` to `ScopedContext`, and validating dispatch across non-software process scenarios.

## Changed Files

SHA-256 hashes are recorded after implementation and before final response.

| File | SHA-256 |
| --- | --- |
| `repo://src/CanDoItAll.Processes.Application/ProcessStepBriefContracts.cs` | `6f19bcd3ad293eac89deb9a80c69a7ce98f2efe13597bbf8e3b649c981361069` |
| `repo://src/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs` | `a619548061366a5b4c50c2a4381e0361218f7fd36f120c25a29f1142004079e8` |
| `repo://src/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs` | `169adf958656d33c8a163d1fcefd0df9786220a683bf593221f03c914bae6291` |
| `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | `a842884c758b7b4c7d47096c9776ca7eeda0166732a5dbde718f158aa94debb9` |
| `repo://src/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs` | `af262526f5c5a0be8c870374c1640ecb3b1125d4548b09b0666a8f25bec89361` |
| `repo://src/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterDriverPackageFactory.cs` | `e775cabf66080fed657d20dc1fac87d7df9b08eabd16f72e156b756a26d226d4` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessLaunchPromptTests.cs` | `e8a073ebeb375df6da7c17cff95e96036ea122a3c2f22c38c3f92160dc204197` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessModuleBoundaryTests.cs` | `9477103344a272fa2ea4571d8a4d3f5dc802d00f855173ac0847dde436811111` |
| `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchApplicationServiceTests.cs` | `29389e72c5fa736c867652626b67213db50725e44794c823c7f3739c07070d64` |

## Command Proof

- `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/transcripts/build-application.txt`
- `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/transcripts/build-module.txt`
- `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/transcripts/test-focused-process-runtime.txt`
- `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/transcripts/static-domain-leak-scans.txt`
- `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/transcripts/validate-bundle-prepared.txt`

## Semantic Invariants

- `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/semantic-invariants.md`

## Source Assertions

- `repo://src/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs` delegates assignment prompt/brief construction through `IProcessStepBriefBuilder`.
- `repo://src/CanDoItAll.Processes.Application/ProcessStepBriefContracts.cs` contains the generic domain-neutral default brief builder.
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs` contains the AgentFramework-specific brief additions and project-structure subprocess guidance.
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs` exposes `ScopedContext`, not `ProjectContext`.

## Failing-First Evidence

Before SB29, generic launch orchestration directly contained:

- `project_structure_process_subprocess_launch`
- `process_step_outcome_result`
- `Project id:` / `Project node id:` prompt text
- `BranchName, RepositoryRoot, SessionId`
- `ChildManagedArtifactRoot`
- output-path evidence guidance

Those strings are now accepted only in the module-owned AgentFramework brief builder and rejected from `CanDoItAll.Processes.Application` by tests/static scans.

## Passing Evidence

- Focused process test slice passed: 19/19.
- `CanDoItAll.Processes.Application` build passed: 0 warnings, 0 errors.
- `CanDoItAll.Modules.Processes` build passed: 0 warnings, 0 errors.
- Static scans found no blocked generic-layer leaks.
- Prepared-stage bundle validator passed.

## Anti-Stub Audit

The implementation is not a stub because production launch assignment creation now calls the injected `IProcessStepBriefBuilder`, the module registers the concrete AgentFramework implementation, and runtime dispatch tests drive the actual scheduler/claim/strategy-result flow across four non-software domains.

## Production Behavior Artifact Matrix

| Signal / state / record | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Generic step brief | `GenericProcessStepBriefBuilder` | `ProcessLaunchApplicationService` | Launch assignment creation for each executable step | `ProcessLaunchPromptTests.Generic_step_brief_includes_runtime_context_without_agent_or_project_guidance` |
| AgentFramework prompt additions | `AgentFrameworkProcessStepBriefBuilder` | AgentFramework process execution adapter | Module DI registration, launch assignment prompt | `ProcessModuleBoundaryTests.Process_launch_application_service_does_not_embed_adapter_specific_step_briefs` |
| Dispatch claim/result events | `ProcessRuntimeEngine` | Runtime projections/outbox | Ready step claim, running transition, completed result, terminal run | `ProcessRuntimeDispatchApplicationServiceTests.ExecuteReady_dispatches_domain_neutral_process_steps_through_same_strategy_path` |
| Scoped adapter driver token | `StandardProcessAdapterDriverPackageFactory` | Driver catalog matching and diagnostics | Adapter package descriptor construction | Static scan in `static-domain-leak-scans.txt` |
