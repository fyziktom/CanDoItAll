# SB06 - Executor Abstractions And Shared Helpers

## Status

- `Completed`

## Objective

Create executor-owned abstractions and shared helper projects so workflow executors are no longer defined, described, invoked, observed, limited, and serialized through MAF-owned code.

## Success Criteria

- Executor contracts, descriptor sources, catalog, invoker abstractions, approval gate, observability/redaction, settings schema helpers, JSON helpers, and policy-limit helpers have executor-owned project homes.
- Executor contracts can be consumed by workflow runtime, default executors, plugins, templates, MAF adapter, and UI display without circular dependencies.
- Existing executor descriptor shapes and ids are preserved.
- Shared helpers are explicit and testable, not hidden static utility dumping grounds.
- Executor invocation failures map to typed workflow diagnostics with retryability, repair hints, redaction, and source/plugin/package/tool context.

## Covered Inputs

- R07, R08, R09, R13, R14, R15, R17.
- Architect note that executors must have their own abstractions and helpers.
- Performance findings around repeated serializer options, LINQ-heavy descriptor materialization, and policy helpers.

## Prerequisites

- SB05 passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Workflows\WorkflowExecutorModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions\WorkflowExecutorContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions\WorkflowExecutorObservabilityContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Core\WorkflowExecutorCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Core\WorkflowExecutorInvoker.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Core\WorkflowExecutorFailureDiagnostics.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Core\WorkflowExecutorJson.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Core\BuiltInWorkflowExecutorDescriptors.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Standard\StandardWorkflowExecutorServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.WorkflowExecutors.Plugins\PluginWorkflowExecutorDescriptorSource.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryMafIntegration.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\CognitiveMemoryModuleServiceCollectionExtensions.cs`

## Deliverables

- `CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions` for executor contracts, descriptor contracts, catalog contracts, invoker contracts, side-effect and policy contracts, approval gate contracts, and descriptor source contracts.
- `CanDoItAll.AgentFramework.WorkflowExecutors.Core` for descriptor materialization helpers, JSON/settings helpers, observability/redaction, typed failure diagnostics, retryability classification, repair-hint helpers, policy limits, catalog composition, and shared test fixtures.
- Tests for descriptor parity, settings schema reflection, invoker failure behavior, diagnostic mapping, observability redaction, side-effect policy, and approval gate semantics.
- Tests or explicit fixtures proving feature modules that provide workflow executors, including Cognitive Memory, can consume the executor abstractions without depending on MAF/Core executor-contract ownership.
- Updated dependency rules and workbook rows for executor foundation.

## Dependency Impact

- SB07 default executor category projects and SB08 plugin adapters depend on this executor foundation. SB10 template loading needs descriptor materialization through these abstractions. SB11 MAF adapter must consume executors through these contracts instead of owning them.

## Validation Depth

- `Critical executor foundation`
- Unit, architecture, descriptor parity, diagnostics, and performance proof.

## Implementation Steps

1. Move executor contracts and descriptor abstractions into executor-owned abstraction project.
2. Move shared helper logic into executor core only when it is reused by at least two executor families or defines a clear boundary.
3. Centralize serializer options/settings schema helpers in a strongly typed way; avoid magic strings beyond external JSON field names.
4. Add descriptor parity tests for current built-in and plugin-projected descriptor examples.
5. Add descriptor parity and registration tests for module-provided executor examples such as Cognitive Memory.
6. Add exception-to-diagnostic tests for missing executor, unavailable descriptor, invalid settings, timeout, cancellation, payload too large, approval denied, and unexpected exception cases.
7. Add redaction/observability tests that include sensitive and non-sensitive values.
8. Add retryability classification tests for invalid settings, timeout, rate limit, missing grant, missing secret, cancellation, and unknown failures.
9. Add architecture tests proving executor abstractions do not depend on MAF, plugin implementation, UI, or default executor implementation projects.
10. Update hosting registration to compose executor foundation services explicitly.

## Scope Exceptions

- Moving concrete default executors is SB07.
- Plugin package loading and plugin executor adapters are SB08.
- UI display adoption is SB12.

## Do Not Do

- Do not create broad generic helper projects unrelated to workflow executors.
- Do not change executor ids or descriptor JSON shapes without explicit compatibility tests.
- Do not add fallback invocation paths for missing executors.
- Do not convert executor exceptions to generic messages without node id, executor id, source kind, attempt count, retryability, redacted technical detail, and repair hint.

## Acceptance Checklist

- [x] Executor abstraction/core projects compile with allowed dependencies.
- [x] Descriptor parity tests pass for built-in and plugin examples.
- [x] Module-provided executor registration/descriptor parity is assigned or covered, including Cognitive Memory.
- [x] Observability redaction and side-effect policy tests pass.
- [x] Failure diagnostic, retryability, and repair-hint tests pass.
- [x] Architecture tests reject forbidden dependencies.
- [x] MAF no longer owns executor contracts/helpers after this phase.

## Execution Notes

- Added `CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions` for executor contracts, descriptor source/catalog/invoker/approval contracts, execution context, and audit contracts.
- Added `CanDoItAll.AgentFramework.WorkflowExecutors.Core` for catalog composition, invoker, policy/side-effect helpers, JSON/settings schema helpers, descriptor factory, redaction/observability helpers, payload policy, typed executor diagnostics, and DI registration.
- Moved executor contract/observability ownership out of `CanDoItAll.AgentFramework.Core\Workflows` and moved `WorkflowExecutorJson` out of MAF.
- Reworked built-in and Cognitive Memory descriptor construction to use `WorkflowExecutorDescriptorFactory`; concrete default executors intentionally remain in MAF for SB07.
- Hosting and `CanDoItAll.Modules.AgentFramework` now compose executor foundation services through `AddWorkflowExecutorCoreServices()`.
- Added direct executor-foundation references for plugin and feature-module consumers and updated the workbook mapping rows for SB06 executor ownership.
- Added `WorkflowExecutorFoundationExtractionTests` covering dependency boundaries, moved files, DI registration, descriptor factory/schema output, built-in and Cognitive Memory descriptor parity, missing-executor diagnostics, redacted invocation diagnostics, approval diagnostics, and plugin/module project references.

## Validation Notes

- Focused executor foundation tests passed: `9/9`.
- Existing executor and hosting regression slice passed: `50/50`.
- Plugin catalog integration slice passed: `29/29`.
- Standalone Gmail, Office365, Docker, and Email plugin builds passed.
- Full dependency-building `dotnet test` against the unit project was blocked by an already-running `CanDoItAll.Web` process locking `src\CanDoItAll.Web\bin\Debug\net10.0` DLLs. Validation was rerun with `dotnet build --no-dependencies` and `dotnet test --no-build` to avoid stopping a process not started by this subbundle.

## Proof Required

- `proof/SB06/manifest.md` with file hashes, build/test transcripts, descriptor parity output, and architecture check transcript.
- `proof/SB06/semantic-invariants.md` covering stable executor ids, descriptor shape compatibility, redaction, retryability, repair hints, side-effect policy, explicit typed invocation failures, and no hidden fallback.
- Semantic Adequacy Gate proof with adversarial missing executor/deserialization cases, positive descriptor materialization cases, and anti-stub audit.

## Browser Validation Logging

- `N/A`. Browser-visible display validation is deferred to SB12.

## Progression Gate

- SB07 and SB08 cannot start until executor abstractions/core helpers compile, pass descriptor parity tests, and prove forbidden dependencies are absent.

## Suggested Agent Prompt

```text
Implement SB06 only. Create executor-owned abstractions and shared helpers after SB05 passes. Preserve descriptor and id compatibility, add parity/redaction/policy/diagnostic/retryability tests, and prove no MAF/UI/plugin implementation dependency leaks into executor contracts. Do not move concrete default or plugin executors yet.
```
