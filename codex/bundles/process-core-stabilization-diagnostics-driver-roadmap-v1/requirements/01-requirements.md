# Requirements

## Hard constraints
- Preserve all existing process runtime behavior.
- Do not move broad runtime orchestration into Core.
- Keep EF, workspace/storage/filesystem, AgentFramework execution, claim lifecycle, transition side effects, finalizer application, projection persistence, and validation orchestration outside Core.
- Do not introduce production driver APIs, driver registries, runtime selectors, manager commands, DI registrations, or execution-capable helper drivers.
- Do not add browser/mobile/small/medium proof. If UI unexpectedly changes, fail the bundle and reopen scope.
- Use larger, meaningful subbundles grouped by phase; avoid micro-subbundle churn.
- Every critical gate must include build/test/source-scan proof.

## Functional preservation requirements
- Route order and route decisions remain identical.
- Subprocess lifecycle status/reason behavior remains identical.
- Subprocess artifact source mapping and ambiguity handling remain identical.
- Artifact expectation matching and recorded satisfaction behavior remains identical.
- Direct-agent execution, retry/provider repair, finalizer handoff, and projection persistence remain module-local and unchanged.

## Improvement requirements
- Address or explicitly classify the current `CA1416` warnings in `ProcessRunAutomationDispatchService.DotnetRunCleanup.cs`.
- Add Core public API surface guardrails.
- Add explainable diagnostics/read-model outputs for pure Core decisions without changing runtime side effects.
- Strengthen module adapter boundaries.
- Prepare driver contract proposal as documentation/test-only readiness, not production implementation.
