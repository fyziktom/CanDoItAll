# SB29 Generic Runtime Dispatcher Leak Repair

## Status

Completed on 2026-06-17.

## Objective

Repair current implementation leaks that made generic process launch and dispatch carry AgentFramework, project-structure, and software-delivery vocabulary in generic Process application and driver contracts.

## Covered Inputs

- User request on 2026-06-17 to analyze current Process implementation for a generic runtime-dispatcher core.
- Requirement that enterprise processes support business analysis, multistep data processing and analysis, quality review, marketing planning, and other non-software domains.
- Requirement that domain-specific behavior lives in drivers, strategies, or module adapters instead of generic Process runtime/dispatcher/core code.
- Existing process module architecture v3 bundle and current implementation after SB20-SB28 runtime completion repair.

## Prerequisites

- SB20-SB28 runtime completion repair has already validated the current project-scoped AgentFramework process run.
- Generic Process projects build before this repair starts.
- Existing focused Process unit tests are available for launch, runtime dispatch, driver boundaries, and module boundaries.

## Exact Source References

- `repo://src/CanDoItAll.Processes.Application/ProcessLaunchApplicationService.cs`
- `repo://src/CanDoItAll.Processes.Application/ProcessStepBriefContracts.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs`
- `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs`
- `repo://src/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterDriverPackageFactory.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessLaunchPromptTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessModuleBoundaryTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchApplicationServiceTests.cs`

## Target Projects / Files

- `src/CanDoItAll.Processes.Application`
- `src/CanDoItAll.Processes.Runtime`
- `src/CanDoItAll.Processes.Core`
- `src/CanDoItAll.Processes.Drivers.Abstractions`
- `src/CanDoItAll.Processes.Drivers.Standard`
- `src/CanDoItAll.Modules.Processes`
- `tests/CanDoItAll.Tests.Unit`

## Rationale

The runtime engine and dispatcher core were mostly generic, but launch orchestration still built an AgentFramework prompt directly. That prompt embedded project ids, project-structure subprocess tools, screenshot/proof/output-path guidance, and `process_step_outcome_result` JSON instructions in `CanDoItAll.Processes.Application`. Those details are valid for the current AgentFramework adapter, but they are not generic process semantics.

## In Scope

- Keep `CanDoItAll.Processes.Runtime` and `ProcessStrategyDispatcher` domain-neutral.
- Extract process step brief construction behind an application port.
- Provide a generic default brief builder without AgentFramework or project-structure language.
- Move AgentFramework/project-structure brief details into `CanDoItAll.Modules.Processes`.
- Rename the unused `ProjectContext` execution adapter kind to a domain-neutral `ScopedContext`.
- Add tests for business analysis, multistep data analysis, quality review, and marketing planning dispatch paths.
- Add static leak scans that fail if adapter/project-specific brief text returns to generic launch orchestration.

## Out Of Scope

- Rewriting project-scoped UI routes.
- Replacing the existing AgentFramework process adapter.
- Repairing historical SB01-SB28 proof metadata that predates the current validator strictness.

## Deliverables

- Generic step brief builder contract and default implementation in the Process application layer.
- Module-level AgentFramework step brief builder that appends project-structure and output-contract guidance outside the generic application layer.
- Domain-neutral execution adapter vocabulary in driver abstractions and standard driver registration.
- Focused unit tests proving generic brief output is domain-neutral and adapter-specific guidance is isolated.
- Runtime dispatch tests for business analysis, data analysis, quality review, and marketing planning process steps.
- Static leak scans for generic Process projects and launch orchestration.
- SB29 proof manifest, semantic invariants, and validation transcripts.

## Dependency Impact

- `CanDoItAll.Processes.Application` gains a port dependency on `IProcessStepBriefBuilder` but does not depend on AgentFramework, project-structure tools, or module-specific prompt contracts.
- `CanDoItAll.Modules.Processes` owns the current AgentFramework-specific brief composition and DI registration.
- Existing launch behavior remains available through the module adapter registration while the generic default builder stays usable by other process hosts.
- Driver abstraction vocabulary changes from project-specific `ProjectContext` to `ScopedContext`; no generic runtime behavior depends on software-project semantics.

## Validation Depth

- Build `CanDoItAll.Processes.Application`.
- Build `CanDoItAll.Modules.Processes`.
- Run focused unit tests covering launch brief boundaries, module boundary scans, driver adapter boundary scans, and runtime dispatch.
- Validate runtime dispatch through four non-software process domains: business market sizing, multistep data analysis, claims quality review, and marketing campaign planning.
- Run static scans proving project-structure and AgentFramework prompt terms do not appear in generic application launch orchestration.
- Run static scans proving generic Process contracts, abstractions, core, builder, runtime, and driver projects do not contain blocked domain vocabulary.

## Implementation Steps

1. Inspect generic Process runtime, dispatcher, launch, driver, and module integration code for project/software-specific vocabulary and behavior.
2. Classify each finding as generic runtime behavior, adapter/module behavior, test-only scenario vocabulary, or proof/documentation-only vocabulary.
3. Extract generic step brief construction behind a strongly typed application port.
4. Implement a domain-neutral default brief builder for non-AgentFramework hosts.
5. Move AgentFramework/project-structure brief details to the module adapter layer.
6. Rename generic adapter vocabulary that exposes project-specific naming.
7. Add focused boundary and runtime dispatch tests for non-software enterprise domains.
8. Run builds, tests, static leak scans, and bundle validation.
9. Re-check repairs for newly introduced domain leaks before closure.

## Do Not Do

- Do not add software, project-structure, Blazor, database, repository, or AgentFramework concepts to generic Process runtime, core, contracts, builder, or driver abstractions.
- Do not remove the existing AgentFramework process adapter behavior; relocate its specificity to the module boundary.
- Do not make tests pass by weakening boundary scans.
- Do not add silent fallback prompt behavior that hides missing adapter registrations.
- Do not repair e2e failures by introducing scenario-specific rules into generic runtime or dispatcher code.

## Implementation Summary

- Added `IProcessStepBriefBuilder`, `ProcessStepBriefBuildRequest`, and `GenericProcessStepBriefBuilder` in `CanDoItAll.Processes.Application`.
- Changed `ProcessLaunchApplicationService` to build runtime assignments through the brief-builder port.
- Added `AgentFrameworkProcessStepBriefBuilder` in `CanDoItAll.Modules.Processes` and registered it through DI.
- Changed `ProcessExecutionAdapterKind.ProjectContext` to `ScopedContext` and updated the standard adapter driver token from `project-context` to `scoped-context`.
- Reworked prompt tests into generic/application and AgentFramework/module split tests.
- Added runtime dispatch validation for four non-software process use cases.

## Acceptance Checklist

- [x] Generic Process application launch orchestration no longer embeds AgentFramework/project-structure prompt text.
- [x] AgentFramework-specific output contract and subprocess tool guidance remains available at the module adapter boundary.
- [x] Driver abstraction no longer exposes `ProjectContext` as a generic adapter kind.
- [x] Runtime dispatch validates across business analysis, multistep data analysis, quality review, and marketing planning step keys.
- [x] Static scans report no new domain leaks in generic process runtime/core/driver/builder projects.
- [x] Focused builds and tests pass.

## Proof Required

- `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/manifest.md`
- `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/semantic-invariants.md`
- `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/transcripts/build-application.txt`
- `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/transcripts/build-module.txt`
- `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/transcripts/test-focused-process-runtime.txt`
- `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/transcripts/static-domain-leak-scans.txt`
- `bundle://proof/SB29-generic-runtime-dispatcher-leak-repair/transcripts/validate-bundle-prepared.txt`

## Browser Validation Logging

- Not required. SB29 changes backend application/module boundaries and unit-testable runtime dispatch behavior; no browser-facing UI behavior changed.

## Progression Gate

- SB29 is complete when the focused builds, focused process tests, static leak scans, and prepared-stage bundle validator pass, with the completed-stage validator gap recorded as historical proof staleness rather than product behavior risk.

## Suggested Agent Prompt

Execute SB29 from `codex/bundles/process-module-architecture-v3/subbundles/29-generic-runtime-dispatcher-leak-repair`. Keep the generic Process runtime, dispatcher, application launch orchestration, and driver abstractions free of project/software-domain leaks while preserving AgentFramework behavior in the module adapter layer. Validate with non-software business, data, quality, and marketing process scenarios, then run leak scans again after any repair.
