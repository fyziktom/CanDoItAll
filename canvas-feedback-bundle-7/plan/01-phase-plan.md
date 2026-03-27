# Phase Plan

1. Prepare and validate the bundle before code changes.
2. Implement subbundle 01:
   - add typed path-presentation data in the Workbench descriptor and adapter
   - render the compact path button, tooltip, and copied-state feedback in shared canvas UI
   - promote file names for file-backed nodes
   - add focused proof for mapping and visible node presentation
3. Implement subbundle 02:
   - route non-preview double-click into a centered quick-action modal
   - derive `Edit` plus the best secondary action from existing Workbench logic
   - wire modal actions back into the current execution path and add focused proof
4. Implement subbundle 03:
   - replace `cfg` with settings iconography
   - clamp or offset the settings overlay below the toolbar
   - prove wide and narrower layout behavior with browser screenshots
5. Update the execution report with exact commands, screenshot paths, and raw-note closure.
