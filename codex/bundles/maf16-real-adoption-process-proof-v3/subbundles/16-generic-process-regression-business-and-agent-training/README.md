# SB16: 16-generic-process-regression-business-and-agent-training

## Goal

Protect generic process runtime.

## Required work

- Run non-software process templates through lint/import/start/read-model tests.
- Add or validate agent-training/improvement process template pattern.
- Ensure artifact validation and MAF adoption do not assume software/build/browser artifacts.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB16` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Protect the generic process runtime from domain-specific Blazor or Tetris behavior.

## Covered Inputs

- RQ10 generic runtime regression boundary.

## Prerequisites

- Process runtime fixes are typed and domain-neutral.

## Exact Source References

- `repo://Templates/Processes/processes/software-delivery/definition.md`

## Deliverables

- Runtime changes remain generic artifact validation behavior.

## Dependency Impact

- SB18 can safely describe the next live test as a template/runbook concern.

## Validation Depth

- Source inspection and focused process tests.

## Implementation Steps

- Keep product-specific proof in templates or tests.
- Avoid domain branching in runtime code.

## Do Not Do

- Do not hard-code Blazor, Tetris, or a particular process profile into core runtime.

## Acceptance Checklist

- Changed runtime code uses generic artifact statuses and diagnostics.

## Proof Required

- SB11 and SB13 source/test proof.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Generic behavior remains intact before final release.

## Suggested Agent Prompt

Keep process runtime generic while using the Blazor/Tetris scenario only as preflight context.
