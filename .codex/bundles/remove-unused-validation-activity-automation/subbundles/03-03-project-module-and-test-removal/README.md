# 03-project-module-and-test-removal

## Status

- `Completed`

## Objective

- Remove the old Validation, Activity, and Automation module projects, all active product connections to them, and tests whose purpose depends on them.

## Success Criteria

- Solution, web, composition, test support, and tool project references no longer include the three old modules.
- Runtime registration and module assembly discovery no longer include the old modules.
- Web navigation, dashboard, layout shortcuts, Workbench projections, right-click actions, and quick actions no longer point to old module routes.
- Module-specific tests are removed or rewritten around supported remaining behavior.

## Covered Inputs

- Main raw goal to remove all three modules.
- Raw note to remove related tests.
- Raw note about project structure right-click menu connections.
- R001, R002, R004, R006, and R009.

## Prerequisites

- SB01 reference workbook exists.
- SB02 removed SchedulerPlanner's Automation dependency.

## Exact Source References

- `repo://CanDoItAll.slnx`
- `repo://src/CanDoItAll.Composition`
- `repo://src/CanDoItAll.Web`
- `repo://src/CanDoItAll.Modules.Workbench`
- `repo://tests`
- `repo://tools/CanDoItAll.ScenarioSeeder`
- `bundle://inventories/unused-module-reference-map.xlsx`

## Deliverables

- Deleted obsolete module project directories.
- Removed project references and runtime registrations.
- Updated web shell/home/layout/workbench source.
- Deleted or updated obsolete tests.
- Direct-reference audit transcript.

## Dependency Impact

- SB04 depends on this cleanup to build and to prove that old routes are not advertised.

## Validation Depth

- Critical product deletion: compile-time audit, route/menu audit, tests, and downstream Browser smoke.

## Implementation Steps

1. Remove active project and solution references to the three modules.
2. Remove composition registration and module assembly discovery for the three modules.
3. Remove shell, layout, home, Workbench, and project-structure connections to old routes/actions.
4. Remove or update tests related to old modules.
5. Verify no direct namespace/project/route references remain outside documented historical artifacts.

## Scope Exceptions

- Historical migration string metadata may remain if it is not active runtime registration.
- Generic validation or automation logic in unrelated modules remains.

## Do Not Do

- Do not delete SchedulerPlanner, workflows, processes, project structure, or generic validation logic.
- Do not add broad replacement layers for removed modules.

## Acceptance Checklist

- `rg` audit is clean for old module namespaces and old advertised routes outside allowed historical artifacts.
- Obsolete tests are gone or updated.
- Project files and solution no longer reference the old modules.

## Proof Required

- `bundle://proof/SB03/transcripts/direct-reference-audit.txt`
- `bundle://proof/SB03/transcripts/deleted-paths.txt`
- Changed-file hash manifest for critical deletion sources.

## Browser Validation Logging

- Route: `/`, `/scheduler`, and project/workbench route if available.
- Viewport: desktop and narrow follow-up if navigation layout changes.
- Evidence: recorded in SB04 after rebuild.

## Progression Gate

- SB04 may start only when direct old-module references are removed or explicitly documented as historical, non-runtime artifacts.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Remove active references to the old Validation, Activity, and Automation modules, clean web and Workbench surfaces surgically, delete obsolete tests, capture direct-reference proof, and stop if any remaining active reference cannot be classified.
```
