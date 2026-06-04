# SB06 — Forced refactor checkpoint: provider boundary cleanup

## Status

Not started.

## Objective

Pause after two product-tool migrations and clean the provider seam before continuing, preventing a second monolith from forming in MAF or Tooling.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-evidence.md`

## Prerequisites

`SB05` must be complete and its progression gate must have passed.

## Exact Source References

- `src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`

## Deliverables

- Provider composition code-size and responsibility review.
- Tooling contracts review: no dependency on product modules, no MAF-only leakage beyond Microsoft.Extensions.AI abstractions.
- Architecture tests for remaining product module references in MAF, with explicit allowed-list and removal plan.
- Decision log for what remains in MAF and why.

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

- `static architecture tests`
- `rg forbidden namespace scans`
- `dotnet build src/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj`
- `dotnet build src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`

## Browser Validation Logging

N/A unless this subbundle unexpectedly changes a rendered UI route. If a rendered route changes, add Playwright MCP route, viewport, assertions, screenshot path, and review notes.

## Progression Gate

No later subbundle may start while MAF contains undocumented product-module references or provider composition has process/project/image-specific naming.

## Suggested Agent Prompt

Implement SB06 only. Read this README, update the relevant source files, run the required proof, record transcripts, update the execution report, and stop at the progression gate before starting the next subbundle.
