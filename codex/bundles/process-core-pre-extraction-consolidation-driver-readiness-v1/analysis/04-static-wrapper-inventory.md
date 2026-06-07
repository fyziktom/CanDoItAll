# Static Wrapper Inventory And Movement Plan

## Scope

`SB025` inspected the current dispatch source after `SB024` closed. This inventory classifies remaining dispatcher wrapper families as pure rule, application helper, infrastructure helper, or compatibility boundary.

No production code was changed for this inventory. Runtime/browser validation remains N/A because this is a runtime/service source classification with no UI, Razor, CSS, JS, TS, image, screenshot, or media scope.

## Classification Rules

| Classification | Meaning | Movement rule |
| --- | --- | --- |
| Pure rule | Deterministic decision, normalization, path/token parsing, DTO matching, or mapper logic with no EF, storage, workspace, filesystem, logging, AgentFramework execution, transition write, service scope, or mutable editor state. | Candidate for movement only when an owning module-local rule already exists and focused parity tests cover the call sites. |
| Application helper | Uses DB, storage, filesystem, service scopes, process transitions, workspace writes, logging, AgentFramework execution, or mutates objects. | Keep application-local behind explicit service/coordinator names. |
| Infrastructure helper | Performs filesystem/storage/project-structure/content IO or other environment interaction. | Do not move into pure rules or future Core candidates. |
| Compatibility boundary | Converts dispatcher nested models, preserves legacy internal test/caller entry points, or bridges dispatcher aliases to module-local DTOs. | Keep named and explicit until dependent callers no longer need compatibility conversion. |

## Source Review

| Source | Finding | Classification | Decision |
| --- | --- | --- | --- |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs` | Converts dispatcher candidates, claims, and execution outcomes to route DTOs and back through named sidecars. | Compatibility boundary | Keep as the route adapter owner. Do not move adapter work into route services or future Core. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs` | Consumes route DTOs and named collaborators without route model adapter calls. | Application helper boundary | No pure-rule movement needed. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs` | Coordinates EF-backed hydration, artifact-input preparation, binding, recovery lookup, and candidate assembly. | Application helper | Keep module-local. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs` | Owns subprocess orchestration and delegates projection persistence/finalizer work to named services. | Application helper | Keep module-local. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs` | Delegates finalizer compatibility work to `ProcessDispatchFinalizerAdapter`. | Application helper with compatibility dependency | Keep adapter dependency explicit. |
| `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs` | Consumes `ProcessDispatchDirectAgentExecutionInput` and delegates execution through the adapter created at the application edge. | Application helper | Keep execution behavior out of pure rules. |

## Wrapper Families

| Wrapper family | Current owner | Classification | SB026 movement decision |
| --- | --- | --- | --- |
| Route eligibility | `ProcessDispatchRouteEligibility` in `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteSnapshot.cs` | Pure rule | Already on owning rule. Dispatcher static facade methods for `IsRunClosedToAutomation`, `IsRunEligibleForDispatchCandidate`, and `IsStepStatusDispatchableForRun` are absent from `ProcessRunAutomationDispatchService.Dispatch.cs`. |
| Subprocess artifact source mapping | `ProcessSubprocessArtifactSourceResolver` and `WorkflowSubprocessArtifactMapper` | Pure rule with workflow/subprocess compatibility semantics | Already on owning resolver. Dispatcher static facades for `ResolveSubprocessSourceArtifact` and `ResolveSubprocessOutputArtifactMappings` are absent from `ProcessRunAutomationDispatchService.Dispatch.cs`. |
| Technical agent binding | `ProcessDispatchTechnicalAgentBindingCoordinator.ApplyProjectStructureReadAccess` plus compatibility forwarding in dispatch service | Application helper | Do not move as pure; it mutates `AgentEditorModel`. |
| Recovery directive query | `ProcessDispatchRecoveryQueryHelper.LoadLatestManualRecoveryDirectiveAsync` plus compatibility forwarding in dispatch service | Application helper | Do not move as pure; it performs DB query work. |
| Artifact quality/project-structure rules | Artifact validation rule classes and dispatcher validation flow | Mixed pure rule plus application aggregation | Candidate only for already-factored pure forwarding with focused artifact validation proof; storage/projection writes stay application-local. |
| Artifact path/text/provider-native rules | `ProcessArtifactPathValidationRules`, `ProcessArtifactTextMatchRules`, `ProcessArtifactProviderNativeVisualValidationRules`, `ProcessProviderNativeBrowserOutputFacts`, and projection coordinators | Mixed pure rule plus compatibility/application boundaries | Move only pure forwarding after parity tests; keep dispatcher-alias overloads until snapshot adapters own conversion. |
| Artifact expectation/satisfaction | `ProcessArtifactExpectationSnapshot`, matcher/resolver, and satisfaction rules | Compatibility boundary over shared pure snapshots | Do not remove dispatcher compatibility blindly. SB023/SB024 proved snapshot parity; further removals need focused tests. |
| Artifact projection utilities | `ProcessRunAutomationDispatchService.ArtifactProjectionUtilities.cs` | Mixed pure projection helpers plus filesystem/transition application behavior | Keep filesystem preflight, directory creation, transition calls, and artifact record operations application-local. |
| Tool validation and provider failure wrappers | Tool validation, session observation, browser-output, and provider-failure helper families | Pure rule plus observation/application orchestration | Candidate only in a later focused move with missing-tool/provider/browser-output tests. |
| Finalizer artifact validation wrappers | Step-completion finalizer validation helper families | Mixed pure rule plus finalizer application behavior | High risk; keep in finalizer boundary for this bundle. |
| Concurrency and retry wrappers | Execution selection, blocking run, no-progress retry, fresh dispatch skip, reusable chat session helpers | Pure rule with retry semantics and application journaling adjacent | Candidate only with retry/no-progress proof from SB021 in a dedicated move. |
| Completion artifact recovery wrappers | Stranded disposition, manager recovery, reusable recovery-run, manager recovery agent helpers | Mixed application helper and pure decision | Keep in dispatcher/recovery scope for this bundle. |
| Host cleanup wrappers | Static web assets alias cleanup and output helper utilities | Infrastructure/application helper | Do not move into pure rules or future Core. |
| Target grounding and operation contract builders | Nested static builders under dispatcher prompt/execution flow | Compatibility/application boundary | Keep module-local; not a Core or driver contract. |

## SB026 Movement Scope

`SB026` is limited to proving the already-safe low-risk movement:

1. Route eligibility callers and tests use `ProcessDispatchRouteEligibility`.
2. Subprocess source artifact mapping callers and tests use `ProcessSubprocessArtifactSourceResolver`.
3. Application/infrastructure wrappers that touch DB, filesystem, transitions, workspace, storage, logging, AgentFramework execution, or mutable editor state remain application-local.

## Gate I Requirements

`SB027` must prove no dispatcher facade resurrection, no side-effect movement into pure rules, no Process Core project, no production process-driver API, no UI/media drift, no stub markers, and focused parity for route eligibility, subprocess artifact mapping, transition/fresh-skip behavior, and wrapper compatibility.
