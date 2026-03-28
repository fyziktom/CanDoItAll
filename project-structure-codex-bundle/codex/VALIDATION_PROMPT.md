# Codex validation prompt

After implementing a task, validate it using this checklist.

## Step 1 — Feature impact
- List the impacted feature IDs from `02_FEATURE_PRESERVATION_MAP.md`.
- Confirm which existing tests already cover them.
- Add missing coverage if the task touches an uncovered risky area.

## Step 2 — Code validation
Run the smallest relevant test set first, then the broader suite for shared code.

## Step 3 — Browser validation
Run or update Playwright/browser checks for:
- overlay behavior,
- toolbox behavior,
- selection window behavior,
- any dialog or modal touched by the task,
- PromptFactory if shared canvas code changed.

## Step 4 — Screenshot validation
Capture or compare screenshots for every visible UI state touched by the task.

## Step 5 — Performance validation
If the task claims performance improvement, capture:
- render counters,
- full rebuild counters,
- state publish counts,
- DB write counts if applicable.

## Pass criteria
Only mark the task complete when:
- all impacted validations pass,
- no preserved feature regressed,
- cross-surface shared behavior still works,
- evidence supports the claimed performance improvement.
