# 01-maf-1-8-upgrade-and-api-delta-gate

## Status

- Status: `Completed`

## Closure Notes

- Upgraded `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, and `Microsoft.Agents.AI.Workflows` to `1.8.0`.
- Upgraded A2A packages to `1.8.0-preview.260528.1`.
- No production adapter code changes were required; the only test change updates the reflection baseline from MAF 1.6 to MAF 1.8.
- Proof manifest: `bundle://proof/SB01/manifest.md`
- Semantic invariants: `bundle://proof/SB01/semantic-invariants.md`

## Objective

Create a clean MAF package/API baseline before deeper runtime changes. Attempt a staged upgrade from the current `1.6.2` MAF stable line to the current compatible NuGet stable line, with compatible A2A preview packages where possible.

## Covered Inputs

- R1: Establish a fresh MAF package/API baseline and either upgrade or record exact blockers in an ADR.
- R10: Decide and document the `BindAsExecutor` versus source-generated executor strategy.
- Prior residual risk: MAF package migration was intentionally deferred by the previous bundle.

## Prerequisites

- Repo branch is `processes-hardening`.
- Gate 0 baseline state is captured before package edits.
- Previous final architecture review is read.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `repo://codex/bundles/workflow-maf-hardening/inventories/03-maf-version-baseline.md`
- `repo://codex/bundles/workflow-maf-hardening/reviews/02-final-architecture-review.md`

## Scope

- Upgrade only MAF-related package references when compatible.
- Fix compile errors caused by API migration with minimal adapter changes.
- If upgrade cannot pass, revert package/code edits and write an ADR with exact blockers.

## Dependency Impact

- SB02-SB08 depend on this package/API baseline before changing runtime behavior.

## Validation Depth

- Package outdated scan, restore/build, and targeted workflow compiler/runtime/executor tests.
- Critical proof requires package transcript, build transcript, targeted test transcript, source assertions, and anti-stub audit.

## Implementation Steps

1. Capture `dotnet list package --outdated --include-prerelease` for affected projects.
2. Upgrade only `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, `Microsoft.Agents.AI.Workflows`, and compatible A2A packages.
3. Restore and build.
4. Fix only API-change compile errors.
5. Run targeted compiler/runtime/executor tests.
6. If blocked, revert package/code edits and write an ADR with exact versions, errors, temporary decision, and retry trigger.

## Do Not Do

- Do not mix HITL, checkpoint, artifact, plugin, or backend behavior changes into this package gate.
- Do not downgrade packages.
- Do not change workflow semantics unless required by API migration.

## Acceptance Checklist

- Package baseline is upgraded or documented with exact blockers.
- Compiler and in-process backend compile against the chosen API line.
- Existing workflow template, compiler, executor, and runtime tests pass or document unrelated pre-existing failures.

## Proof Required

- Package scan transcript.
- Restore/build transcript.
- Targeted test transcript.
- ADR if not upgraded.
- `bundle://proof/SB01/manifest.md` and `bundle://proof/SB01/semantic-invariants.md`.

## Browser Validation Logging

- No browser proof is required unless package migration changes UI-visible workflow surfaces.

## Progression Gate

- Continue to SB02 only after MAF packages are upgraded with passing restore/build/tests, or an ADR captures exact blockers and the temporary compatibility decision.

## Suggested Agent Prompt

Use this subbundle to attempt the MAF package upgrade in isolation, prove the compiler/runtime adapter state, and avoid mixing runtime behavior changes into the baseline gate.
