# Browser proof playbook

## Primary route

- Dedicated WebGL sandbox route, proposed: `/webgl/process-workbench`

## Viewports

- `1600x900`
- `1366x768`
- `430x932`

## Required template passes

1. `customer-onboarding`
2. `architecture-decision-governance`
3. `branching-code-review`

## Required action passes

- initial fit-view screenshot,
- switch template screenshot,
- node move before/after,
- connection mutation before/after,
- reset-after-edit screenshot,
- runtime-exported image capture.

## Screenshot review questions

- Are all important labels readable without zoom gymnastics?
- Did depth reduce clutter or create occlusion?
- Are edges easier to follow than in the dense 2D baseline?
- Is the default camera understandable to a first-time reviewer?
- After movement, is the scene still coherent?
- On narrow view, does the route still orient the reviewer quickly?

## Logging rule

Every screenshot pair should be tied to:

- template key,
- camera/view preset,
- viewport size,
- semantic scene snapshot ID or timestamp,
- pass/fail note.
