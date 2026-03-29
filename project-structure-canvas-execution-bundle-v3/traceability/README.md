# Traceability folder

This folder contains machine-readable support material for the execution bundle.

## Files

- `features.csv`  
  Canonical feature preservation list used by the task briefs and validation gates.

- `tasks_to_features.csv`  
  Feature/task cross-reference showing which tasks must keep which features green.

- `hotspots.csv`  
  Prioritized performance and maintainability hotspots with evidence and target tasks.

- `runtime_files.csv`  
  Key runtime/shared-canvas files with current role, problem summary, and target split/move.

- `js_function_inventory.csv`  
  Inventory of top-level functions currently found in `canvasWorkbenchInterop.js` with a recommended destination module in the split-source layout.

- `component_inventory.csv`  
  CanvasLib component inventory with category classification and example consumers.

- `existing_test_inventory.csv`  
  Relevant existing tests that Codex should preserve, expand, or use as a regression base.

- `current_gaps.json`  
  JSON summary of the main unresolved gaps after the previously applied bundle.

- `old_to_new_canvaslib_mapping.csv`  
  Suggested current-path to target-path mapping for the CanvasLib reorganization.

## Honesty note

These files were generated from a static source audit of the uploaded repository snapshot.  
Build/runtime validation still needs to be executed by Codex in an environment with the project toolchain.
