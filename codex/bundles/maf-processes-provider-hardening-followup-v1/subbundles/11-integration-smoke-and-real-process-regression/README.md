# SB11 — Integration smoke and real process regression

## Status

Not started.

## Objective

Prove that the provider seam remains compatible with real app composition, zero-provider MAF startup, process automation evidence semantics, and at least one non-UI process runtime path.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-evidence.md`

## Prerequisites

`SB10` must be complete and its progression gate must have passed.

## Exact Source References

- `tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj`
- `tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`
- `src/CanDoItAll.Web/CanDoItAll.Web.csproj`

## Deliverables

- Entry smoke from prior SB09 rerun.
- Provider composition integration proof after all migrations.
- Process outbox, receipt semantics, artifact-lineage proof.
- One lightweight real process-run smoke using existing test harness or seeded scenario, not manual claim text.

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

- `dotnet test tests/CanDoItAll.Tests.Unit`
- `dotnet test tests/CanDoItAll.Tests.Integration --filter Process`
- `dotnet build CanDoItAll.slnx`

## Browser Validation Logging

N/A unless this subbundle unexpectedly changes a rendered UI route. If a rendered route changes, add Playwright MCP route, viewport, assertions, screenshot path, and review notes.

## Progression Gate

Final docs and closure cannot start until real provider composition and process evidence smoke pass on the final source shape.

## Suggested Agent Prompt

Implement SB11 only. Read this README, update the relevant source files, run the required proof, record transcripts, update the execution report, and stop at the progression gate before starting the next subbundle.
