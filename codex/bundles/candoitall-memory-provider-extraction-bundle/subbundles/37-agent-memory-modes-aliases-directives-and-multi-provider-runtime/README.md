# 37 Agent Memory Modes Aliases Directives And Multi-provider Runtime

## Status

- `Completed`

## Execution Outcome

- Typed `Disabled`, `Automatic`, and `ExplicitDirective` settings, ordered provider bindings, aliases, automatic inclusion, and required/optional behavior now live at the Models/Agent Framework Memory boundary.
- A dedicated `CanDoItAll.AgentFramework.Memory` project owns parsing, planning, bounded stable-order fan-out, merging, context contribution, tools, and workflow execution; the former module-owned runtime types were removed.
- Leading `/mem:<alias>` directives are authorized and removed from provider/model text, unknown aliases fail before dispatch, attachments/metadata are preserved, and returned memory is framed as untrusted data.
- Final focused evidence: AgentFramework.Memory, AgentFramework Core, Modules.AgentFramework, and Modules.Memory each built with 0 warnings/0 errors; AgentFramework.Memory tests passed 22/22; focused Unit memory/MAF/workflow filters passed 29/29; focused Components agent-memory/provider-editor filters passed 24/24. Microsoft.OpenApi NU1903 appeared only in test dependency graphs and is not reported as a clean test build.
- SB40 completed the real-host desktop/narrow agent Memory proof and the real contributor-handler-driver-ledger two-provider/explicit/unknown-alias seam.

## Objective

- Give each agent typed memory settings, selectable provider aliases, explicit automatic-versus-directive invocation behavior, and a deterministic multi-provider runtime implemented outside the Blazor module.

## Success Criteria

- Agent create/edit/read round-trips typed memory settings without hand-editing `ConfigurationJson`.
- One agent can bind zero, one, or many providers by strongly typed provider ID and unique alias.
- Invocation mode is explicit: disabled performs no memory work, automatic queries configured automatic bindings, and explicit-directive queries only providers requested with `/mem:<alias>`.
- Directives are parsed at safe token boundaries, authorized against the agent bindings, removed from provider/model query text, and never interpreted as arbitrary configuration.
- Multi-provider results are deterministic and provider-labelled, with explicit required-versus-optional failure semantics and no cross-provider fallback.
- Runtime integration lives in a dedicated Agent Framework memory project; the Razor module owns only UI/composition.

## Covered Inputs

- R09
- R10
- R11
- R12
- R20
- R22
- R23
- R24
- R27

## Prerequisites

- SB36 completed with provider allowlist, deny-fallback, operation ownership, and modular handler gates passing.

## Exact Source References

- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/02-csharp-dependency-direction.md`
- `bundle://architecture/03-csharp-pattern-selection-records.md`
- `bundle://architecture/04-csharp-testability-plan.md`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Memory/AgentMemoryAccessMetadata.cs`
- `repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Tools/MemoryAgentRuntimeToolProvider.cs`
- `repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Context/MemoryAgentContextContributor.cs`
- `repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Routing/MemoryMafProviderPolicyResolver.cs`
- `repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/WorkflowExecutors/MemoryWorkflowExecutor.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Editors/EditorModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Catalog/AgentFrameworkWorkspaceCatalogService.Agents.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Memory/AgentMemoryConfigurationMapper.cs`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentContextContributionContracts.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MemoryAgentContextContributorTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MemoryAgentRuntimeToolProviderTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MemoryMafIntegrationCheckpointTests.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/MemoryProvidersPageTests.cs`

## Deliverables

- Add a strongly typed `AgentMemoryInvocationMode` with `Disabled`, `Automatic`, and `ExplicitDirective` values and no string comparisons.
- Add typed agent memory settings with explicit tool access, context contribution policy, failure policy, and a collection of provider bindings containing `MemoryProviderInstanceId`, normalized alias, automatic-inclusion flag, and required/optional behavior.
- Move the settings contract and strict JSON codec to a lower Agent Framework model/configuration owner that the editor and runtime can both reference; malformed settings produce an actionable validation error instead of silently becoming defaults.
- Migrate valid legacy configuration deterministically: legacy context-contribution enablement maps to `Automatic`; absence of all legacy/generic settings remains `Disabled`; invalid provider IDs or duplicate aliases fail validation.
- Add typed memory settings to `AgentEditorModel`, agent catalog read/write, and `AgentDetailsDialog` through a focused BaseLib-backed memory settings component with a provider picker loaded from the generic provider catalog.
- Add a dedicated `CanDoItAll.AgentFramework.Memory` project for directive parsing, binding resolution, multi-provider query orchestration, context contribution, runtime tool exposure, and workflow memory adaptation; move logic out of `CanDoItAll.Modules.AgentFramework`.
- Implement `/mem:<alias>` parsing for one or more aliases, case-insensitive alias lookup with a canonical stored form, deterministic de-duplication, input sanitization, and typed unknown/disallowed/disabled diagnostics.
- Define precedence: an explicit directive narrows the current request to the named authorized bindings; `ExplicitDirective` without a directive does no memory work; `Automatic` without a directive uses bindings marked automatic; `Disabled` never dispatches.
- Execute authorized multi-provider queries through the shared `IMemoryOperationHandler`, with bounded concurrency, stable binding-order output, provider labels/provenance, operation correlation, and explicit handling of required versus optional provider failure.
- Prevent the legacy `AgentMemoryRecord` workspace contributor from attaching duplicate memory when generic provider settings are configured; retain compatibility only for agents with no generic memory configuration.
- Remove the memory capability grouping from the broad workspace catalog partial cluster by extracting a cohesive legacy-memory catalog collaborator or merging only genuinely cohesive catalog code; do not add new generic provider behavior to `AgentFrameworkWorkspaceCatalogService.Memory.cs`.

## Dependency Impact

- SB38 consumes the typed runtime request context emitted by this integration and maps it through each transport.
- SB39 uses the resulting agent/session/project identity to enforce the native provider boundary.
- SB40 browser and E2E proof depends on settings round-trip, directive sanitization, and real multi-provider calls established here.

## Validation Depth

- `Critical MAF runtime and agent-settings UI foundation`

## C# Architecture Impact

- Agent-memory runtime code moves from a Razor module into a dedicated non-UI project with configuration, directives, routing, context, tools, and workflow namespaces.
- The editor model gains a typed memory settings property, while JSON remains a persistence format behind one strict codec.
- The current single-provider contributor becomes an agent-level orchestrator over the generic one-provider operation facade; it does not move fan-out into Memory Application.

## Boundary Ownership

- Agent Framework Models owns serializable agent memory settings and validation-compatible editor state.
- Agent Framework Memory owns interpretation, directive parsing, binding resolution, multi-provider orchestration, MAF context/tool/workflow adapters, and its DI registration.
- Memory Application remains provider-neutral and handles one selected provider operation at a time.
- `CanDoItAll.Modules.AgentFramework` owns the Blazor settings panel and invokes typed application/catalog services only.

## Dependency Direction

- `AgentFramework.Models -> Memory.Abstractions` is allowed only for strongly typed provider IDs and memory configuration contracts.
- `AgentFramework.Memory -> AgentFramework.Models/Core/Tooling + Memory.Application/Abstractions` is allowed.
- `Modules.AgentFramework -> AgentFramework.Memory` is allowed for UI and composition.
- `Memory.Application -> AgentFramework.*`, `AgentFramework.Memory -> Modules.AgentFramework`, and any Agent Framework project -> native Cognitive Memory are forbidden.

## Pattern Decision

- Use a small parser value-result type for `/mem:` syntax and a routing policy for mode/binding authorization.
- Use an orchestrator for multi-provider fan-out because ordering, bounded concurrency, partial failure, and merged provenance form one cohesive behavior.
- Use an adapter for MAF context/tool/workflow seams to the orchestrator and generic operation facade.
- Do not use reflection, raw dictionary settings, magic tags, string provider IDs, or a service locator.

## Testability Contract

- The directive parser and routing policy have pure table-driven tests covering valid, malformed, duplicate, unknown, disabled, and injection-shaped inputs.
- The orchestrator accepts explicit handler/time/concurrency dependencies and is directly testable with recording providers.
- Settings codec tests cover legacy migration, round-trip, malformed JSON, invalid identifiers, duplicate aliases, and unknown-property preservation policy.
- Component tests exercise the real typed editor binding and catalog save/reload path, not a component-local view model only.

## Partial Class Policy

- Runtime settings, parser, resolver, orchestrator, contributor, and tool provider are independent non-partial top-level types in cohesive folders/namespaces.
- Razor `.razor` plus `.razor.cs` partial code-behind is allowed for the settings panel and dialog.
- Do not extend the `AgentFrameworkWorkspaceCatalogService` partial cluster for new memory behavior; remove or isolate its memory capability grouping as part of this phase.

## Architecture Proof Required

- Add project-reference guards proving Memory Application has no Agent Framework dependency and Agent Framework Memory has no module/native dependency.
- Show pre/post type ownership for every class moved from `AgentTools`, `Context`, `MemoryIntegration`, and memory workflow executor folders.
- Capture partial-class and namespace audits proving runtime integration is no longer implemented in the Razor module or broad catalog partial.
- Record the pattern/testability checkpoint in `bundle://reviews/csharp-architecture-gate.md` before SB38.

## Implementation Steps

1. Turn SB35 mode/directive/multi-provider characterization cases into named red tests.
2. Add typed settings, binding aliases, validation, and strict legacy-aware codec at the agreed model boundary.
3. Create the Agent Framework Memory project and move/adapt runtime memory integration without changing generic Memory Application ownership.
4. Implement directive parser, routing policy, and bounded deterministic multi-provider orchestrator.
5. Wire MAF context, runtime tool, and workflow paths through the orchestrator and shared generic handler; gate legacy workspace memory.
6. Add typed agent editor/catalog persistence and the BaseLib memory settings panel.
7. Add unit, component, composition, and project-boundary tests; perform large and narrow browser validation of agent settings.

## Scope Exceptions

- Full workspace/project/execution propagation into HTTP/MCP envelopes is owned by SB38.
- External service authorization and native recall filtering are owned by SB39.

## Do Not Do

- Do not encode aliases, modes, provider IDs, or commands as unvalidated strings.
- Do not query every registered provider; query only bindings authorized by the agent settings and current mode/directive.
- Do not leave `/mem:` tokens in text sent to the model or provider.
- Do not swallow malformed settings and silently enable or disable memory.
- Do not implement fan-out inside generic Memory Application or the Blazor component.

## Acceptance Checklist

- Agent settings UI can add, remove, reorder, validate, save, reload, and display multiple provider bindings.
- Zero bindings and disabled mode perform zero calls and show a clear configured state.
- Automatic mode calls only automatic authorized bindings in stable order.
- Explicit-directive mode without `/mem:` performs zero calls; `/mem:memory1` calls only alias `memory1`.
- Multiple directives call each authorized alias once, and merged context identifies each provider and preserves provenance.
- Unknown, malformed, duplicate, disabled, or disallowed aliases return typed diagnostics and zero unauthorized calls.
- Required provider failure fails predictably; optional provider failure is visible and does not erase successful labelled results.
- Tool, workflow, and context routes all reach the shared generic operation handler and ledger.
- Legacy workspace memory is not attached in addition to configured generic provider context.

## Proof Required

- Create `proof/SB37/manifest.md` and `proof/SB37/semantic-invariants.md` with hashes and portable source/transcript/screenshot references.
- Failing-first proof: capture absent invocation-mode/UI/directive behavior and current single-provider-only contributor failures against pre-SB37 production code.
- Positive proof: save/reload an agent with two provider aliases, run automatic and explicit prompts through the real MAF contribution path, and correlate provider-labelled output with two real ledger operations.
- Negative proof: run disabled, no-directive, unknown-alias, duplicate-alias, disallowed-provider, malformed-settings, and required-provider-failure cases and prove unauthorized providers receive zero calls.
- Anti-stub proof: show the sanitized prompt, parsed aliases, selected binding IDs, real handler calls, ledger rows, and merged MAF context; component-only state or fabricated context packs do not qualify.
- Run focused Unit, Memory, Components, composition, architecture-guard, and build gates.
- Capture maximized and narrow screenshots of the Memory tab with zero, one, multiple, and invalid bindings.

## Browser Validation Logging

- Target the agent create/edit dialog Memory tab through its real host route.
- Run maximized desktop and narrow-width passes; verify provider rows, aliases, mode controls, validation messages, and save/reload behavior.
- Use Playwright actions to add two providers, choose modes, provoke duplicate/unknown validation, save, reopen, and assert persisted values.
- Record screenshot paths and review whether controls remain legible, errors are associated with the correct binding, and no raw JSON or secret value is exposed.

## Progression Gate

- SB38 may start only after typed settings round-trip, mode routing, directive sanitization, multi-provider handler/ledger correlation, UI browser proof, dependency guards, and the SB37 architecture checkpoint all pass.

## Suggested Agent Prompt

```text
Implement SB37 only. Add typed agent memory settings, a dedicated Agent Framework memory project, safe /mem:<alias> routing, and deterministic multi-provider orchestration through the shared handler. Preserve the UI/runtime/generic boundaries, capture real MAF and browser proof, and stop if the architecture gate cannot pass.
```
