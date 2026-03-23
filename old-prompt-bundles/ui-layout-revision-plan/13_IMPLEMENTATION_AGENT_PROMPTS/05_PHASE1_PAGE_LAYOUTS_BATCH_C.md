# Prompt: Phase 1 Page Layouts Batch C

You are finishing the standard page migration set.

## Scope

- Prompt Gallery
- Activity
- Automation
- Project Calendar

## Read First

- `../06_PAGE_BY_PAGE_REVIEW.md`
- `../08_LAYOUT_PATTERNS_AND_ASCII_SKETCHES.md`
- `../09_RECOMMENDED_DESIGN_RULES.md`

## Goals

1. Bring remaining standard pages onto the shared composition system.
2. Improve search, status scanning, and history readability.
3. Improve the project calendar page shell without changing the calendar engine.

## Requirements

- Prompt Gallery should gain clearer library structure and history framing
- Activity should distinguish no-query, no-results, and timeline states
- Automation should surface summary and filter affordances if practical
- Project Calendar should gain stronger loading/detail treatment without changing the calendar JS wrapper

## Do Not Touch

- prompt factory internals
- project structure internals
- calendar engine behavior beyond shell/detail framing

## Expected Outputs

- migrated `PromptGalleryPage.razor`
- migrated `ActivityPage.razor`
- migrated `AutomationPage.razor`
- improved `ProjectCalendarPage.razor`

## Self-Check Before Finishing

- Does Prompt Gallery present editing and history more clearly?
- Does Activity now feel like a search tool instead of a loose form plus timeline?
- Does Automation surface operational state faster?
- Is Project Calendar still functionally unchanged but easier to scan?

