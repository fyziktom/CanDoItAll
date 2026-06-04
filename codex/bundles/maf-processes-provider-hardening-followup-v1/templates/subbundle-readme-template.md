# {{id}} — {{title}}

## Status

Not started.

## Objective

{{objective}}

## Covered Inputs

- `inputs/00-original-request.md`
- `inputs/01-branch-review-summary.md`
- `inputs/02-source-evidence.md`

## Prerequisites

{{prereqs}}

## Exact Source References

{{refs}}

## Deliverables

{{deliverables}}

## Dependency Impact

{{dependency_impact}}

## Validation Depth

This subbundle requires source assertions, targeted tests, and proof transcripts. Compile-only proof is not sufficient when tool-provider behavior changes.

## Implementation Steps

{{steps}}

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

{{checklist}}

## Proof Required

{{proof}}

## Browser Validation Logging

N/A unless this subbundle unexpectedly changes a rendered UI route. If a rendered route changes, add Playwright MCP route, viewport, assertions, screenshot path, and review notes.

## Progression Gate

{{gate}}

## Suggested Agent Prompt

Implement {{id}} only. Read this README, update the relevant source files, run the required proof, record transcripts, update the execution report, and stop at the progression gate before starting the next subbundle.
