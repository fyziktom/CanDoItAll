# SB03 — MAF provider composition policy refactor gate

## Status

Not started.

## Objective

Refactor MAF provider composition after SB02 so approval wrapping, duplicate tool detection, provider failure diagnostics, and tool attachment logging are generic provider-seam policies rather than process-specific helpers.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-evidence.md`

## Prerequisites

`SB02` must be complete and its progression gate must have passed.

## Exact Source References

- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`

## Deliverables

- Rename process-specific helper names such as WrapInternalProcessMutationTool to provider-neutral naming.
- Extract provider composition helper class or partial section if MafAgentRuntime.Capabilities.cs grows further.
- Preserve exact behavior of approval wrapping and duplicate tool name failure.
- Regression tests for provider ordering, failure diagnostics, duplicate tool names, and no-provider behavior.

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

- `dotnet test tests/CanDoItAll.Tests.Unit --filter RuntimeToolProviderComposition`
- `rg "WrapInternalProcessMutationTool|AttachInternalProcessToolsAsync|CreateProcessToolBuilder" src/CanDoItAll.AgentFramework.Maf`

## Browser Validation Logging

N/A unless this subbundle unexpectedly changes a rendered UI route. If a rendered route changes, add Playwright MCP route, viewport, assertions, screenshot path, and review notes.

## Progression Gate

No product-provider migration may start until the MAF composition code is provider-neutral in naming, tests, diagnostics, and proof transcripts.

## Suggested Agent Prompt

Implement SB03 only. Read this README, update the relevant source files, run the required proof, record transcripts, update the execution report, and stop at the progression gate before starting the next subbundle.
