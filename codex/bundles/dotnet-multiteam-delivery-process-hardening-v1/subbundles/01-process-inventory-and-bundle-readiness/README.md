# Process inventory and bundle readiness

## Status

- `Ready`

## Objective

Ground the work in the current repository and produce an implementation-ready bundle before editing process templates.

## Covered Inputs

- R01 through R10 as inventory and planning inputs.

## Prerequisites

- none

## Exact Source References

- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/processes/app-pages-screenshot-set/definition.json`
- `repo://Templates/Agents/teams/dotnet-delivery/members/dotnet-solution-architect/settings.json`

## Deliverables

- Current-state analysis.
- Normalized requirements.
- Traceability matrix.
- Phase plan and self-review.

## Dependency Impact

- All implementation subbundles depend on this inventory. If the current-state analysis misses the existing subprocess model, later edits can duplicate or bypass working process infrastructure.

## Validation Depth

- Critical foundation

## Implementation Steps

1. Read the bundle workflow and preparation rules.
2. Search the repository for process templates, subprocess references, and agent permission files.
3. Save raw request and normalize requirements.
4. Define subbundles, gates, risks, and validation expectations.
5. Run prepared-stage bundle validation.

## Scope Exceptions

- JavaScript process separation is excluded from this bundle.

## Do Not Do

- Do not edit production process templates in this subbundle.
- Do not run the software-delivery process.

## Acceptance Checklist

- Raw request is preserved.
- Requirements map back to raw notes.
- Phase plan has dependency map, critical subbundles, and gates.
- Bundle validator passes or failures are repaired.

## Proof Required

- Prepared-stage validator transcript in execution report.
- Source inventory references are present.

## Browser Validation Logging

- N/A. This subbundle does not change browser-visible behavior.

## Progression Gate

- Prepared-stage validation passes before SB02 starts.

## Suggested Agent Prompt

```text
Prepare and validate this bundle only. Do not edit process templates or run the software-delivery process.
```
