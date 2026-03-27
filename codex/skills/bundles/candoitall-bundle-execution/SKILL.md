---
name: candoitall-bundle-execution
description: Execute an existing CanDoItAll bundle or subbundle phase by phase with code changes, proof, and bundle status updates. Use when a bundle already exists and Codex needs to implement it safely without skipping scope, validation, screenshots, tests, or follow-up documentation.
---

# CanDoItAll Bundle Execution

Treat the bundle as the contract. Implement one subbundle at a time and update the bundle with proof as the code changes land.

This skill is for delivery, not planning. If the bundle is missing, unclear, or unvalidated, switch back to bundle preparation before changing feature code.

## Required Flow

1. Read the root `README.md`, `plan`, `traceability`, and the selected subbundle README before editing code.
2. Confirm which requirements or notes the subbundle owns. Do not bleed scope from later phases into the current pass.
3. Audit the exact source references named by the bundle and the nearby tests they imply.
4. Implement the smallest correct change set for the current subbundle only.
5. Validate with the proof defined in the bundle:
   - component or unit tests
   - builds
   - Playwright or browser checks
   - screenshots when UI is involved
6. Update the bundle after each completed subbundle:
   - subbundle status
   - execution report
   - proof artifacts
   - follow-up items when something cannot be closed in the current phase
7. Move to the next subbundle only after the current one is proven.

## Execution Rules

- Do not silently widen scope because two subbundles touch the same file.
- Do not declare a subbundle done without the proof listed in its README.
- Do not rewrite the bundle casually while implementing. Only change the bundle when reality requires a better statement of scope, proof, or follow-up work.
- If a subbundle is blocked, create a concrete follow-up subbundle or item instead of hiding the gap in prose.
- Keep the bundle and the code in sync. A stale bundle is a process defect.

## UI Work Rule

For Blazor or other UI-heavy subbundles:

- use `candoitall-watch-playwright-loop` when hot-reload and browser proof matter
- use browser truth, not assumption, for layout and visibility fixes
- capture screenshots for meaningful UI changes
- ask the validation questions from `references/ui-validation-questions.md`

## Bundle Update Rule

After finishing a subbundle, update at least:

- the subbundle README status section if it has one
- `reviews/01-execution-report.md`
- any proof artifact paths
- any remaining risks or follow-up subbundles

## References

- Read [references/execution-loop.md](references/execution-loop.md) for the subbundle-by-subbundle delivery sequence.
- Read [references/proof-and-status-updates.md](references/proof-and-status-updates.md) before updating the bundle after execution.
- Read [references/ui-validation-questions.md](references/ui-validation-questions.md) for the UI inspection checklist.

## Exit Condition

The execution is only complete when the code, tests, browser evidence, and bundle status all agree.
