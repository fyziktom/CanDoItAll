# SB29 Semantic Invariants

## Invariant ID: SB29-GEN-001

Source raw note: User requested generic runtime-dispatcher core for processes and explicitly warned that software-development-specific logic must not leak into generic parts.

Expected behavior: Generic Process application launch orchestration delegates step brief creation through a port and does not embed AgentFramework, project-structure subprocess, software-delivery evidence, or structured-output prompt details.

Disallowed shallow implementation: Keep `BuildStepPrompt` in `ProcessLaunchApplicationService` and only rename strings, or hide project-structure instructions behind helper methods in the same generic application service.

Failing-first test: Existing implementation had `project_structure_process_subprocess_launch`, `process_step_outcome_result`, project id/node id prompt text, and output-path evidence guidance in `repo://src/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`.

Passing test: `repo://tests/CanDoItAll.Tests.Unit/ProcessLaunchPromptTests.cs` and `repo://tests/CanDoItAll.Tests.Unit/ProcessModuleBoundaryTests.cs` passed in `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/transcripts/test-focused-process-runtime.txt`.

Changed source files:
- `repo://src/CanDoItAll.Processes.Application/ProcessStepBriefContracts.cs`
- `repo://src/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`

Production assertions: `ProcessLaunchApplicationService` depends on `IProcessStepBriefBuilder`; `GenericProcessStepBriefBuilder` produces a domain-neutral brief; `AgentFrameworkProcessStepBriefBuilder` owns the AgentFramework/project-structure additions in `CanDoItAll.Modules.Processes`.

Red-team negative case: Static scan rejects the exact leaked strings in `CanDoItAll.Processes.Application`; see `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/transcripts/static-domain-leak-scans.txt`.

Downstream dependency check: `CanDoItAll.Processes.Application` and `CanDoItAll.Modules.Processes` builds passed with no warnings or errors.

## Invariant ID: SB29-GEN-002

Source raw note: Processes must serve business analysis, multistep data processing and analysis, quality processes, marketing planning/execution, and other enterprise work.

Expected behavior: The same runtime dispatch application path schedules, claims, invokes strategy, submits result, and completes a process step without depending on software-development vocabulary.

Disallowed shallow implementation: Validate only Tetris/software delivery templates or only string-level prompt output.

Failing-first test: Previous bundle closure validated a TetrisGame project-scoped path and left broader non-Blazor scenarios as future expansion.

Passing test: `ProcessRuntimeDispatchApplicationServiceTests.ExecuteReady_dispatches_domain_neutral_process_steps_through_same_strategy_path` passed for business-market-sizing, multistep-data-analysis, claims-quality-review, and marketing-campaign-planning.

Changed source files:
- `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchApplicationServiceTests.cs`

Production assertions: The test drives `ProcessRuntimeDispatchApplicationService` through scheduler, claim, strategy factory resolution, result submission, event emission, and terminal completion with domain-specific step keys only as data.

Red-team negative case: The dispatch test asserts runtime event payloads do not contain `Tetris`; static scans cover broader project/software terms.

Downstream dependency check: Focused process unit test slice passed: 19/19.

## Invariant ID: SB29-GEN-003

Source raw note: Specific parts are acceptable as drivers or strategies, but generic driver abstractions must not expose domain-specific adapter vocabulary.

Expected behavior: The driver abstraction uses a domain-neutral scoped-context adapter kind instead of `ProjectContext`.

Disallowed shallow implementation: Keep `ProcessExecutionAdapterKind.ProjectContext` and only avoid using it.

Failing-first test: `ProcessExecutionAdapterKind.ProjectContext` existed in `repo://src/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs` and mapped to `project-context` in the standard driver package factory.

Passing test: `ProcessExecutionAdapterBoundaryTests` and static scans passed in `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/transcripts/test-focused-process-runtime.txt` and `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/transcripts/static-domain-leak-scans.txt`.

Changed source files:
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs`
- `repo://src/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterDriverPackageFactory.cs`

Production assertions: `ProcessExecutionAdapterKind.ScopedContext` and the `scoped-context` driver token are generic adapter vocabulary; project-specific scope semantics remain integration concerns outside the generic driver contracts.

Red-team negative case: Static scan rejects `ProcessExecutionAdapterKind.ProjectContext`, `ProjectContext`, and `project-context` in driver abstraction, standard driver, runtime, and core projects.

Downstream dependency check: `CanDoItAll.Processes.Application`, `CanDoItAll.Modules.Processes`, and the focused unit slice passed.

## Production Behavior Artifact Matrix

| Signal / state / record | Producer | Consumer | Lifecycle | Negative-test citation |
| --- | --- | --- | --- | --- |
| Generic step brief | `GenericProcessStepBriefBuilder` | `ProcessLaunchApplicationService` runtime assignment creation | Built per executable step during launch preparation | `ProcessLaunchPromptTests.Generic_step_brief_includes_runtime_context_without_agent_or_project_guidance` |
| AgentFramework step brief additions | `AgentFrameworkProcessStepBriefBuilder` | AgentFramework process execution adapter through runtime assignment prompt | Registered by `AddProcessesModule`; produced at launch assignment time | `ProcessModuleBoundaryTests.Process_launch_application_service_does_not_embed_adapter_specific_step_briefs` |
| Domain-neutral dispatch events | `ProcessRuntimeDispatchApplicationService` and `ProcessRuntimeEngine` | Projection catch-up and runtime read models | Step scheduled, claimed, run, completed, and terminal run emitted | `ProcessRuntimeDispatchApplicationServiceTests.ExecuteReady_dispatches_domain_neutral_process_steps_through_same_strategy_path` |
| Scoped adapter kind | `ProcessExecutionAdapterKind.ScopedContext` | Standard adapter driver package factory | Driver descriptor token generation | Static scan in `static-domain-leak-scans.txt` |

