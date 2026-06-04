# SB09 — Runtime tool-provider observability and receipt tagging

## Status

Not started.

## Objective

Make provider ownership visible in progress logs, tool receipts, diagnostics, and proof artifacts so later driver packs can be traced to their provider and purpose.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-evidence.md`

## Prerequisites

`SB08` must be complete and its progression gate must have passed.

## Exact Source References

- `src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `src/CanDoItAll.AgentFramework.Core/README.md`
- `src/CanDoItAll.AgentFramework.Maf/README.md`

## Deliverables

- Provider key/name included in attach progress messages and testable diagnostics.
- Tool ownership metadata carried into receipt trace where available, without breaking existing receipt schema.
- Backward-compatible receipt projection for existing runs.
- Documentation for provider observability.

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

- `tool receipt semantics tests`
- `runtime tool provider composition tests`
- `process receipt semantics smoke`
- `dotnet test tests/CanDoItAll.Tests.Integration --filter Receipt`

## Browser Validation Logging

N/A unless this subbundle unexpectedly changes a rendered UI route. If a rendered route changes, add Playwright MCP route, viewport, assertions, screenshot path, and review notes.

## Progression Gate

Receipt schema changes must be backward compatible; existing process artifact lineage and receipt semantics tests must pass.

## Suggested Agent Prompt

Implement SB09 only. Read this README, update the relevant source files, run the required proof, record transcripts, update the execution report, and stop at the progression gate before starting the next subbundle.
