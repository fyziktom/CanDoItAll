# SB05 — Image-generation tool provider extraction from MAF

## Status

Not started.

## Objective

Move image-generation internal tool attachment behind the same runtime provider seam so MAF stops accumulating hard-coded first-party product/tool surfaces.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-evidence.md`

## Prerequisites

`SB04` must be complete and its progression gate must have passed.

## Exact Source References

- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`

## Deliverables

- Inventory current image-generation tool names, access metadata, and approval policy.
- New image-generation runtime tool provider in the most appropriate owning module after source inventory.
- Remove AttachInternalImageGenerationToolsAsync from MAF after parity proof.
- Document any remaining provider-native image dependency that must stay in MAF.

## Dependency Impact

Moderate dependency impact; downstream proof must still include regression checks.

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

- `rg "CreateImageGenerationToolBuilder|AttachInternalImageGenerationToolsAsync" src tests`
- `dotnet test tests/CanDoItAll.Tests.Unit --filter ImageGeneration`
- `dotnet build CanDoItAll.slnx`

## Browser Validation Logging

N/A unless this subbundle unexpectedly changes a rendered UI route. If a rendered route changes, add Playwright MCP route, viewport, assertions, screenshot path, and review notes.

## Progression Gate

Image tools must still be available to eligible agents, still approval-wrapped when required, and MAF must not contain image-generation-specific attach code after closure.

## Suggested Agent Prompt

Implement SB05 only. Read this README, update the relevant source files, run the required proof, record transcripts, update the execution report, and stop at the progression gate before starting the next subbundle.
