# SB01 — Branch hygiene, entry proof, and merge-scope cleanup

## Status

Not started.

## Objective

Establish a clean entry baseline after the first decoupling bundle, separate runtime source changes from bundle/proof artifact churn, and prevent accidental removal of unrelated historical bundles before the branch is merged.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-evidence.md`

## Prerequisites

None; this is the entry gate.

## Exact Source References

- `CanDoItAll.slnx`
- `README.md`
- `codex/bundles/maf-processes-decoupling-bundle-v1/reviews/01-execution-report.md`
- `codex/bundles/maf-processes-decoupling-bundle-v1/reviews/02-final-red-team-review.md`

## Deliverables

- Clean branch-diff inventory grouped by runtime source, tests, docs, and codex bundle artifacts.
- Explicit decision record for whether historical codex/bundles deletions are intentional; restore accidental deletions before downstream work.
- Entry proof rerun transcript set reused from SB09: hidden dependency scan, provider/policy tests, process evidence smoke, and full build.

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

- `git diff --name-status development...maf-processes-refactor`
- `rg hidden MAF process dependency scan`
- `dotnet build CanDoItAll.slnx`

## Browser Validation Logging

N/A unless this subbundle unexpectedly changes a rendered UI route. If a rendered route changes, add Playwright MCP route, viewport, assertions, screenshot path, and review notes.

## Progression Gate

No downstream subbundle may start until the branch diff is classified and accidental codex/bundles deletions are either reverted or explicitly approved in a decision note.

## Suggested Agent Prompt

Implement SB01 only. Read this README, update the relevant source files, run the required proof, record transcripts, update the execution report, and stop at the progression gate before starting the next subbundle.
