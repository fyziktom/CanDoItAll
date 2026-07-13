# Context Budget And Artifact Packaging

## Status

- `Completed`

## Objective

Bound the context passed between process steps by packaging artifact manifests, summaries, and retrieval handles by default, while allowing full content only through explicit driver policy.

## Covered Inputs

- R05, R11, R12, R14
- US06, US08
- EX10, EX13, EX16
- Architect notes that software-development processes may pass too many code files as artifacts and cause the next agent to lose track.

## Prerequisites

- SB02 connected artifact lineage contract is available.
- SB03 step contract retrieval is available.
- Driver policy boundary from SB06 is either complete or coordinated for packaging policy.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepBriefBuilder.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeStepAssignments.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/processes/dotnet-development-slice/steps/implement-code-change.md`
- `repo://Templates/Processes/processes/dotnet-feature-function-implementation/definition.json`

## Deliverables

- Bounded context package model or policy using artifact manifests, summaries, retrieval handles, content budget, and sensitivity metadata.
- Default behavior that avoids dumping arbitrary product file content into downstream agent context.
- Driver policy hook for domains that explicitly need full content.
- Tests for oversized changed-file sets, summary/handle packaging, sensitivity filtering, and explicit full-content policy.
- Updated prompts/tool instructions to prefer retrieval handles and fresh step contract.

## Dependency Impact

- SB08 regression proof must confirm downstream agents receive bounded but sufficient context.
- This reduces the context-loss risk described in the architect notes.

## Validation Depth

- `Process-critical context closure`

## Implementation Steps

1. Define generic context budget facts and package shape.
2. Use SB02 lineage and SB03 retrieval contracts as package inputs.
3. Implement default manifest/summary/handle packaging.
4. Add explicit driver policy for full content and reject accidental unbounded dumps.
5. Update evidence source/brief builder to use package output.
6. Add tests for oversized context and explicit policy.
7. Update proof manifest and execution report.

## Scope Exceptions

- Does not design every domain-specific package summary.
- Does not implement a new UI unless package inspection is surfaced.
- Does not remove all historical templates that pass broad context unless touched by tests.

## Do Not Do

- Do not inline all changed product files by default.
- Do not hide missing artifact refs behind summaries.
- Do not expose sensitive content beyond policy.
- Do not add software-development-specific package rules to generic runtime.

## Acceptance Checklist

- Default downstream context contains manifests/summaries/retrieval handles, not unbounded file dumps.
- Oversized package blocks with actionable diagnostic or switches to handles according to policy.
- Driver can explicitly request full content when justified.
- Tests prove package stays within budget and retains required artifact identity.
- Finalizer/agent instructions point to retrieval rather than stale inlined content.

## Proof Required

- `bundle://proof/SB07/manifest.md` with changed-file hashes, commands, and package examples.
- `bundle://proof/SB07/semantic-invariants.md` describing context budget invariants.
- Test with oversized changed-file set.
- Test proving required artifact refs remain available through handles.
- Test proving explicit driver full-content policy is required for inlining.

## Browser Validation Logging

- Route: `N/A unless package/projection UI is touched`
- Viewports: if UI touched, large desktop plus affected responsive width
- Playwright evidence: package viewer or process detail renders without leaking content if UI changed
- Screenshots: record concrete paths if UI changed
- Review questions: can the operator inspect package identity and budget without sensitive data leakage?

## Progression Gate

- SB08 may proceed only when bounded package behavior is proven.
- No default path may dump arbitrary product files into downstream context.

## C# Architecture Impact

Introduces cross-boundary package policy. Generic runtime can know budgets and handles; domain-specific selection and summarization belongs in drivers/modules.

## Boundary Ownership

Runtime/Core own generic package metadata. Drivers own domain-specific full-content policy. Module integration owns AgentFramework prompt rendering.

## Dependency Direction

No generic runtime references to project files, .NET source concepts, or AgentFramework prompt types.

## Pattern Decision

Use policy composition with typed package records. Avoid inheritance trees for package types unless existing code already uses that pattern.

## Testability Contract

Package policy tests must assert item counts, sizes/budgets, handles, sensitivity, and required artifact identity.

## Partial Class Policy

Do not add context packaging into an adapter partial. Extract from evidence source or prompt builder if needed.

## Architecture Proof Required

- Package tests.
- Source assertions for no domain leakage.
- Anti-stub audit proving packages are built from actual lineage/contract facts.

## Suggested Agent Prompt

```text
Implement SB07 only. Bound process step context with manifests, summaries, retrieval handles, and explicit driver full-content policy. Prove oversized product/code artifacts do not get dumped into downstream context by default.
```
