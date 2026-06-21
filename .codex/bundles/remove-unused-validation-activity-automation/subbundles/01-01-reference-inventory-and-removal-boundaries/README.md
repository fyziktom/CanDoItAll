# 01-reference-inventory-and-removal-boundaries

## Status

- `Completed`

## Objective

- Produce and preserve the pre-removal reference map, then classify direct references into remove, refactor, or keep-by-scope decisions.

## Success Criteria

- `bundle://inventories/unused-module-reference-map.xlsx` exists and is cited from the bundle.
- Direct module references are mapped before product edits.
- Generic unrelated terms are separated from actual old-module dependencies.

## Covered Inputs

- Raw note requiring all references to be mapped before deletion.
- Raw note warning about project-structure right-click menu connections.
- R003 and R009.

## Prerequisites

- Port `5032` stop command has completed.
- Repository checkout is readable.

## Exact Source References

- `repo://CanDoItAll.slnx`
- `repo://src`
- `repo://tests`
- `bundle://inventories/unused-module-reference-map.xlsx`

## Deliverables

- Reference workbook and preview image.
- Current-state analysis and scope inventory updates.
- Keep/remove boundary for historical migrations and generic terminology.

## Dependency Impact

- SB02 depends on the SchedulerPlanner Automation-dependency findings.
- SB03 depends on the Workbench, UI, composition, and test reference categories.

## Validation Depth

- Critical foundation: workbook artifact proof, direct-reference summary, and downstream boundary check.

## Implementation Steps

1. Generate or refresh the workbook.
2. Inspect the workbook for invalid formula-like source text and artifact readability.
3. Record direct-reference categories in analysis and inventory files.
4. Mark generic references as non-removal targets unless tied to the old module projects.

## Scope Exceptions

- Historical EF migrations may remain if they do not block build or runtime startup.

## Do Not Do

- Do not delete product code in this subbundle.
- Do not treat every generic use of validation, activity, or automation as an obsolete module reference.

## Acceptance Checklist

- Workbook exists.
- Preview image exists.
- Current-state analysis names the high-risk hidden reference areas.
- Traceability maps the reference-map requirement to SB01.

## Proof Required

- `bundle://inventories/unused-module-reference-map.xlsx`
- `bundle://inventories/unused-module-reference-map-preview.png`
- Spreadsheet inspection showing no formula-injection findings.

## Browser Validation Logging

- N/A: this subbundle does not change browser-visible behavior.

## Progression Gate

- SB02 and SB03 may start only after the workbook exists and the keep/remove boundary is recorded.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Verify the reference workbook exists, summarize direct old-module references, keep generic terms scoped out, update bundle analysis and inventory, and stop before product-code deletion.
```
