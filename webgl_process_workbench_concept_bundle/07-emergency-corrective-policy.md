# Emergency corrective policy

## Trigger policy

A corrective subbundle must be executed immediately if any of the following happen:

- the universal library stops being universal,
- the runtime boundary drifts into Blazor/server round trips,
- the projected 3D scene becomes harder to read than the current 2D baseline with no clear mitigation,
- semantic automation cannot prove actual move/connect mutations,
- architecture Gate A or Gate B fails.

## Available corrective playbooks

- `_corrective-renderer-boundary-reset`
- `_corrective-scene-contract-and-layout-reset`
- `_corrective-automation-and-proof-reset`

## Mandatory sequence

1. stop all downstream work,
2. execute the corrective subbundle,
3. refresh the blocked proof,
4. rerun the failed gate,
5. continue only after an explicit pass.

## What corrective work is allowed to do

Corrective work may:

- refactor boundaries,
- split files or services,
- simplify the scene contract,
- reduce scope temporarily if the concept direction was too ambitious.

Corrective work may not:

- hide the original failure,
- weaken the promised proof contract,
- silently skip the failed requirement.
