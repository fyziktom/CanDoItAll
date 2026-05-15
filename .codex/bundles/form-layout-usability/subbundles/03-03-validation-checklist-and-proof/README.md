# 03 Validation Checklist And Proof

## Status

- Subbundle status: `Completed`

## Objective

Produce the final `.xlsx` checklist, complete screenshot/proposal comparisons, and close the raw request note by note.

## Covered Inputs

- Analyze all forms across the app.
- Generate imagegen proposal per form screenshot.
- Maintain xlsx checklist with file references.
- Validate each change with screenshot comparison.

## Prerequisites

- Subbundle 01 closure gate passed.
- Subbundle 02 closure gate passed or explicit blocked rows exist.

## Exact Source References

- `C:\repositories\CanDoItAll\.codex\bundles\form-layout-usability\inventories\01-scope-inventory.md`
- `C:\repositories\CanDoItAll\.codex\bundles\form-layout-usability\reviews\01-execution-report.md`
- `C:\repositories\CanDoItAll\output`

## Deliverables

- `.xlsx` checklist with rows for inventory, proposals, code files, validation screenshots, comparison status, and closure.
- Final browser analytics rows.
- Raw-note closure table with `Solved`, `Partially solved`, or `Not solved`.

## Dependency Impact

- Final closure depends on this proof.
- Missing workbook or screenshot rows block completion.

## Validation Depth

- Render/inspect workbook visually.
- Run final build/test proof.
- Run `scripts/validate_bundle.py --stage completed`.

## Implementation Steps

1. Use workspace spreadsheet dependencies and artifact-tool to build the workbook.
2. Add checklist tables and status formatting.
3. Render workbook preview and repair obvious visual issues.
4. Run final validator and update execution report.

## Scope Exceptions

- If a form is unreachable because of runtime data state, mark it as `Inventoried / Not reachable in current run` with source evidence and follow-up status.

## Do Not Do

- Do not ship a CSV instead of `.xlsx`.
- Do not count generated image proposals as proof.

## Acceptance Checklist

- [x] Workbook exists and opens/renders.
- [x] Every implemented visual change has proposal and validation screenshot paths.
- [x] Raw notes are closed note by note.
- [x] Final bundle validator passes or documented blockers remain.

## Proof Required

- Workbook path: `C:\repositories\CanDoItAll\output\form-layout-usability\form-layout-checklist.xlsx`.
- Workbook render proof: artifact-tool render/inspect pass completed and spreadsheet error scan matched 0 entries.
- Final validator output: recorded in the final task transcript.

## Browser Validation Logging

- Update `reviews/01-execution-report.md` with final screenshot rows and analytics review.
- Keep generated image proposal paths separate from shipped browser proof paths.

## Progression Gate

- Pass only if workbook, screenshots, proposals, build proof, and raw-note closure agree.
- Block final closure when any implemented change lacks validation screenshots or comparison status.

## Suggested Agent Prompt

Build the final `.xlsx` checklist from inventory and proof artifacts, visually verify it, update the execution report, run final validators, and close each raw note with evidence.
