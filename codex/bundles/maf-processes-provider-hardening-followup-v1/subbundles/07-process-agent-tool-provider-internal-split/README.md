# SB07 — ProcessAgentRuntimeToolProvider internal split

## Status

Not started.

## Objective

Refactor the Processes-owned provider into smaller maintainable slices without extracting process core or changing the 23-tool public surface.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-evidence.md`

## Prerequisites

`SB06` must be complete and its progression gate must have passed.

## Exact Source References

- `src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs`
- `src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`
- `src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`

## Deliverables

- Split provider into catalog/factory, definition tools, run tools, template tools, access guard, and DTO files or partials.
- Keep all tool names and signatures stable unless a dedicated migration note explains otherwise.
- Keep provider registration unchanged from consumers viewpoint.
- Add maintainability guard against another 900+ line single provider file.

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

- `dotnet test tests/CanDoItAll.Tests.Unit --filter ProcessAgentRuntimeToolProvider`
- `dotnet test tests/CanDoItAll.Tests.Integration --filter ProcessRuntimeProvider`
- `process tool exact-name parity test`

## Browser Validation Logging

N/A unless this subbundle unexpectedly changes a rendered UI route. If a rendered route changes, add Playwright MCP route, viewport, assertions, screenshot path, and review notes.

## Progression Gate

The provider split is accepted only when exact tool parity, access denial tests, and policy/capability registry tests still pass.

## Suggested Agent Prompt

Implement SB07 only. Read this README, update the relevant source files, run the required proof, record transcripts, update the execution report, and stop at the progression gate before starting the next subbundle.
