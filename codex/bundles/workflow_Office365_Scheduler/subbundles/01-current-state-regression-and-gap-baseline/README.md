# 01-current-state-regression-and-gap-baseline

## Objective

Confirm the current pushed state after the workflow executor catalog bundle and create a reliable baseline before Office365/Scheduler changes.

## Must Verify

- MAF packages remain on the 1.8 package line.
- Existing executor-catalog tests pass.
- `storage.file`, `json.transform`, `markdown.render`, `utility.delay`, `human.approval`, `http.fetch`, and `source.ingest` remain registered/runnable as expected.
- `command.process` remains planned/unavailable.
- Workflow template manifest loads without duplicate keys.
- Scheduler can list workflow targets and launch an existing workflow with raw `InputJson`.
- Office365 plugin currently has only category-based download and mark processed.

## Implementation Steps

1. Capture current commit, package baseline, restore, build.
2. Run targeted tests from the previous workflow executor catalog bundle.
3. Add or update an inventory file listing:
   - Office365 executors currently available.
   - Scheduler target launch path.
   - Scheduler UI input fields.
   - Existing workflow templates relevant to project tasks/assets.
4. Add failing-first tests for the missing Office365-by-address executor and scheduler typed parameter form. These tests may remain failing until later subbundles, but they must be documented.

## Acceptance Checklist

- Baseline proof is present.
- Existing executor catalog behavior is not regressed.
- The missing Office365/Scheduler capabilities are documented as failing-first or TODO evidence, not hand-waved.
