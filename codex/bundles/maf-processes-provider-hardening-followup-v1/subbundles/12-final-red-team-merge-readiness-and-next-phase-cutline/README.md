# SB12 — Final red-team, merge readiness, and next-phase cutline

## Status

Not started.

## Objective

Close this follow-up with an adversarial review and a precise cutline for the next bundle, which may then prepare process contracts/core extraction foundation.

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-evidence.md`

## Prerequisites

`SB11` must be complete and its progression gate must have passed.

## Exact Source References

- `CanDoItAll.slnx`
- `src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj`
- `src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs`

## Deliverables

- Final hidden dependency scans for Processes, project-structure, image-generation hard-coded attach paths.
- Final tool parity/policy/provider tests.
- Branch hygiene closure and merge note.
- Next-phase readiness document that explicitly permits only contracts/core foundation, not driver packs yet.

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

- `final full build`
- `targeted unit/integration reruns`
- `bundle validator prepared/completed when available`
- `manual red-team checklist`

## Browser Validation Logging

N/A unless this subbundle unexpectedly changes a rendered UI route. If a rendered route changes, add Playwright MCP route, viewport, assertions, screenshot path, and review notes.

## Progression Gate

Bundle may close only when red-team confirms no hidden MAF product-tool coupling was reintroduced and no process-core/driver work was smuggled into this phase.

## Suggested Agent Prompt

Implement SB12 only. Read this README, update the relevant source files, run the required proof, record transcripts, update the execution report, and stop at the progression gate before starting the next subbundle.
