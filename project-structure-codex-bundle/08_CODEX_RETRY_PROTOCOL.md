# Codex retry protocol

This protocol is mandatory for every task in this bundle.

## Core rule

Codex is not allowed to stop at “the code was changed.”  
Codex must continue until the task passes its validation gates or until a clearly documented blocker is found.

## Required loop

For each task:

1. **Inspect first**
   - read the task brief,
   - read impacted files,
   - map impacted feature IDs from `02_FEATURE_PRESERVATION_MAP.md`.

2. **Implement narrowly**
   - make the smallest viable change that satisfies the task,
   - avoid broad unrelated cleanup.

3. **Run targeted validation**
   - unit/bUnit tests for impacted behavior,
   - browser tests for impacted overlays or scene behavior,
   - screenshot/artifact capture if the task changes visible UI,
   - performance counters if the task claims a render or persistence improvement.

4. **Review failures**
   - if any test fails, fix the implementation or fix the test only when the product behavior truly changed intentionally,
   - if screenshots fail, investigate whether the difference is a regression or an intentional approved change,
   - if performance counters do not improve, revisit the implementation.

5. **Rerun the impacted suite**
   - rerun all impacted gates, not only the previously failing one.

6. **Record the result**
   - list impacted features,
   - note what changed,
   - note what tests and browser scenarios were rerun,
   - note any remaining follow-up that is intentionally deferred.

## Explicit fail states

A task is considered failed if any of these are true:

- existing green behavior regressed,
- PromptFactory broke after a shared-canvas change,
- wheel/click/context-menu leakage remains in overlays,
- DB writes still happen during active viewport or window movement where the task was meant to remove them,
- full scene rebuild counters remain unchanged on a task that was supposed to eliminate them,
- screenshots show obviously broken layout or missing UI.

## Screenshot rule

For visible UI changes, screenshots are not optional.  
They are required because this workbench has many overlay combinations and shared-canvas behaviors that are easy to break without noticing in unit tests.

## Do-not-merge rule

Do not merge or mark complete a task that:
- improved one metric but broke a preserved feature,
- reduced code size by deleting behavior that was supposed to stay,
- passes code tests but fails browser regression gates.
