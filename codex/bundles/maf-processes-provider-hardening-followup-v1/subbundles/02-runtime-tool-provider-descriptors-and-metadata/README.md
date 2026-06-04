# SB02 — Runtime tool-provider descriptors and metadata contract

## Status

Not started.

## Objective

Harden the new IAgentRuntimeToolProvider seam so providers expose stable identity, domain tags, tool ownership, operation kind, approval expectation, and supported purposes before more product tool providers are migrated.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-evidence.md`

## Prerequisites

`SB01` must be complete and its progression gate must have passed.

## Exact Source References

- `src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`
- `src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs`
- `src/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderPurpose.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`

## Deliverables

- Provider descriptor model, for example AgentRuntimeToolProviderDescriptor.
- Optional tool metadata model without breaking existing raw AITool providers.
- Provider metadata tests for stable key, duplicate provider key handling, null/empty provider key failure, and operation-kind classification.
- No process-core extraction.

## Dependency Impact

Critical foundation for downstream work.

## Validation Depth

This subbundle requires source assertions, targeted tests, and proof transcripts. Compile-only proof is not sufficient when tool-provider behavior changes.

## Implementation Steps

1. Open every exact source reference and confirm current branch shape.
2. Create or update the smallest set of source files needed for this subbundle.
3. Preserve existing public tool names and policy behavior unless this subbundle explicitly owns the change.
4. Run targeted proof before broader build proof.
5. Record source assertions, test transcripts, and any reopen triggers.
6. Update the execution report and stop at the progression gate.

## Scope Exceptions

- No process-core extraction.
- No process driver packs.
- No unrelated UI work.

## Do Not Do

- Do not silently rename or drop existing tools.
- Do not weaken approval or access policy.
- Do not use broad cleanups that touch unrelated modules without explicit inventory.
- Do not mark placeholder proof as passed.

## Acceptance Checklist

- [ ] Source inventory for this slice is recorded.
- [ ] Implementation is limited to this subbundle scope.
- [ ] Tool parity/access/approval behavior is proven where applicable.
- [ ] Static dependency scans are updated where applicable.
- [ ] Targeted tests pass.
- [ ] Full or relevant project build pass is recorded.
- [ ] Execution report is updated.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Unit --filter AgentRuntimeToolProvider`
- `dotnet test tests/CanDoItAll.Tests.Unit --filter AgentToolInvocationPolicy`

## Browser Validation Logging

N/A unless this subbundle unexpectedly changes a rendered UI route. If a rendered route changes, add Playwright MCP route, viewport, assertions, screenshot path, and review notes.

## Progression Gate

A provider without metadata must still work through an adapter during migration, but new first-party providers must declare descriptor metadata and tests must fail on duplicate provider keys.

## Suggested Agent Prompt

Implement SB02 only. Read this README, update the relevant source files, run the required proof, record transcripts, update the execution report, and stop at the progression gate before starting the next subbundle.
