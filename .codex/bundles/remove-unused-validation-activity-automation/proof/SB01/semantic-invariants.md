# SB01 Semantic Invariant Contract

## Invariant ID

- Invariant ID: `RM-001`

## Source raw note

- Map all Validation, Activity, and Automation references before deleting them, preferably as XLSX.

## Expected behavior

- A durable workbook inventory exists before code deletion and is safe to open.

## Disallowed shallow implementation

- Deleting projects before producing a reference map, or emitting an unreadable workbook.

## Failing-first test

- failing-first: N/A - process/non-production proof; this subbundle creates inventory evidence rather than production behavior.

## Passing test

- `proof/SB01/transcripts/workbook-inspection.txt`

## Changed source files

- None; this subbundle produced bundle inventory artifacts.

## Production assertions

- Workbook exists at `inventories/unused-module-reference-map.xlsx`.
- Preview exists at `inventories/unused-module-reference-map-preview.png`.

## Red-team negative case

- The workbook inspection would fail if the workbook, preview, or inspection NDJSON were missing.

## Downstream dependency check

- SB02 and SB03 used the workbook inventory to remove SchedulerPlanner, Workbench, composition, UI, and test references.
