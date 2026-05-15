# 01 MAF Context Contribution Boundary

## Status

- Ready.

## Objective

- Add a general MAF context contribution extension point that future modules can use without editing private MAF context-provider construction for each feature.

## Covered Inputs

- PR-FR-001, PR-FR-002, PR-FR-006, PR-NFR-002, PR-NFR-004, and PR-NFR-005.
- Source finding that current MAF context composition is private and provider-specific.

## Prerequisites

- None.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Context.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowExecutorContracts.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj

## Deliverables

- Contributor contract and result model.
- Registration pattern for contributors.
- MAF composition update that preserves existing static/RAG/Mem0/workspace behavior.
- Tests for ordering, skipping, failure reporting, policy context, and cancellation.

## Dependency Impact

- Cognitive Memory later registers a contributor through this boundary.
- Existing MAF context providers remain compatible.
- Workflow and agent integration can inspect contributor traces.

## Validation Depth

- Critical foundation.
- Unit tests and targeted integration tests are required.
- Source review must confirm no Cognitive Memory-specific hardwire was introduced.

## Implementation Steps

- Choose the contract location with the least coupling.
- Define contributor id, order, request, policy context, cancellation, and result records.
- Adapt current context composition to enumerate registered contributors.
- Preserve existing context behavior as built-in contributors or compatibility path.
- Add tests for deterministic order, skip, failure, and cancellation.

## Do Not Do

- Do not implement Cognitive Memory.
- Do not add a `CognitiveMemoryContextProvider` special case to private MAF internals.
- Do not remove existing workspace memory fallback.
- Do not swallow contributor exceptions without traceable failure state.

## Acceptance Checklist

- Contributors can be registered without editing provider-specific MAF branches.
- Existing context features still work.
- Contributor failures are explicit and traceable.
- The future Cognitive Memory MAF subbundle can consume this boundary.

## Proof Required

- Targeted unit test output.
- Targeted integration or registration test output.
- Source diff showing contributor boundary is generic.

## Browser Validation Logging

- No browser proof is required unless an agent UI route changes unexpectedly.
- If UI changes happen, record route, viewport, and screenshot in `reviews/01-execution-report.md`.

## Progression Gate

- Proceed to Cognitive Memory MAF integration only after this boundary is generic, tested, and behavior-compatible.

## Suggested Agent Prompt

- Implement the generic MAF context contribution boundary only, preserving current behavior and proving contributor ordering, skip/failure reporting, policy context, and cancellation.
