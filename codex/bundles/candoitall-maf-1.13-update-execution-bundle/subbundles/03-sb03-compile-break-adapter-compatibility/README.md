# SB03 Compile Break Adapter Compatibility

## Status

Completed.

## Objective

Fix package-induced compile breaks while preserving current CanDoItAll runtime behavior and architecture boundaries.

## Covered Inputs

- `bundle://inputs/original-prep/docs/03-breaking-change-risk-map.md`
- `bundle://inputs/original-prep/docs/08-file-touch-plan.md`
- `bundle://architecture/00-csharp-current-state-inventory.md`
- `bundle://architecture/03-csharp-pattern-selection-records.md`

## Prerequisites

- `SB02` package update and restore result are complete.
- Remaining errors are compile/API compatibility errors, not unrelated cleanup.
- Package decisions have been recorded.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderStreamingRunner.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowCompiler.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafInProcessWorkflowExecutionBackend.cs`

## Deliverables

- Minimal adapter compatibility fixes.
- Build transcript.
- New or adjusted focused tests only when behavior or helper code changes.
- Source assertions for governance invariants.

## Dependency Impact

- `SB04` depends on this diff for architecture drift review.
- `SB05` depends on build success and bounded source changes.

## Validation Depth

- Build proof is mandatory.
- Unit proof required for any new helper/adapter.
- Source scan proof required for governance behavior.
- Semantic Adequacy Gate required because this is a critical foundation.

## Implementation Steps

1. Run build and capture compile errors.
2. Triage errors in this order: restore/package graph, missing types/namespaces, agent/session/run options, streaming content/update types, skill-source approval/caching/disposal, FileAccess/FileMemory signatures, A2A, workflow adapter, tests.
3. Fix only package-induced errors.
4. Use typed compatibility helpers only when they reduce duplication or isolate SDK drift.
5. Run build again.
6. Run targeted tests for any changed behavior.
7. Record source assertions for approval, finalizer, provider gate, session, context, and process-tool invariants.

## Scope Exceptions

- Large runtime decomposition is deferred.
- New MAF features are deferred.
- Process direct-tool ambiguity is not fixed here.

## Do Not Do

- Do not remove approval wrappers.
- Do not weaken required finalizer behavior.
- Do not replace finalizer output with free-form JSON parsing.
- Do not remove provider lane gates or timeouts.
- Do not make provider-specific behavior global.
- Do not move process-domain behavior into MAF.
- Do not add `ProcessAgentRuntimeToolProvider`.

## Acceptance Checklist

- `dotnet build CanDoItAll.slnx --configuration Release --no-restore` succeeds or records a specific blocker.
- Source changes are bounded to allowed adapter/test files unless the bundle is repaired.
- Governance invariants are asserted.
- Any new helper has direct unit tests and negative tests.
- No broad fallback mechanism silently hides package/API errors.

## Proof Required

- `proof/SB03/manifest.md`.
- `proof/SB03/semantic-invariants.md`.
- Failing-first build transcript.
- Passing build transcript or blocker transcript.
- Source assertion transcript.
- Test transcript for changed behavior.
- Anti-stub audit transcript.

## Browser Validation Logging

- N/A unless compile fixes alter UI-visible component behavior; if that happens, repair the bundle before using browser proof here.

## Progression Gate

- `SB04` may start. Build proof, source assertions, focused tests, and semantic invariant proof exist under `bundle://proof/SB03/`.

## C# Architecture Impact

- Highest architecture risk subbundle.
- Any new adapter/helper must be justified by `architecture/03-csharp-pattern-selection-records.md`.

## Boundary Ownership

- MAF SDK compatibility stays in MAF adapter projects.
- Workflow SDK compatibility stays in workflow adapter project.
- Process and module projects remain outside compile fixes unless build errors prove otherwise.

## Dependency Direction

- No new project references unless the bundle is repaired and CodeAnalytics proof is recorded.

## Pattern Decision

- Prefer simple call-site update.
- Use Adapter only for external SDK drift.
- Use Factory/Builder only if construction/selection changed and is justified.

## Testability Contract

- New helpers must be unit-testable without constructing `MafAgentRuntime`.
- Negative tests must prove unsupported cases fail explicitly.

## Partial Class Policy

- No new final partial class files.
- Temporary partials are blocked unless the bundle is repaired with removal proof.

## Architecture Proof Required

- Changed-file list.
- Source assertions that old runtime classes did not gain unrelated responsibilities.
- Pattern selection update if new helper type is introduced.
- Testability proof for extracted behavior.

## Suggested Agent Prompt

Execute `SB03` only. Fix compile breaks caused by the package update with minimal adapter-compatible changes. Preserve governance behavior. Run build and focused tests for changed behavior. Create artifact-backed proof and stop before broader validation.
