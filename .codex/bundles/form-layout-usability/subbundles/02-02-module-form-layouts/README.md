# 02 Module Form Layouts

## Status

- Subbundle status: `Completed`

## Objective

Apply targeted layout/grouping improvements to high-density product forms after the shared foundation is proven.

## Covered Inputs

- Dense forms that need topical grouping or subtabs.
- Form areas that still waste space after shared fixes.
- Long-text module fields that need specific larger sizes or full-width placement.

## Prerequisites

- `01-01-shared-form-foundation` closure gate passed.
- Workbook has baseline rows for targeted module forms.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessDefinitionForm.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessStepEditorForm.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessRoleEditorForm.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Components\CandidatePipeline.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CrmHr\Components\OpportunityEditor.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentDetailsDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`

## Deliverables

- Targeted form layouts use available width without unrelated broad rewrites.
- Dense process forms are split into topical tabs or clearly separated form sections.
- Textareas for notes, summaries, instructions, JSON, prompts, and policy text are readable by default.

## Dependency Impact

- Depends on shared form foundation.
- May require reopening subbundle 01 if repeated module failures show shared CSS is insufficient.

## Validation Depth

- Build after code edits.
- Browser screenshots for changed module form regions.
- Compare each changed screenshot to its imagegen proposal in the workbook.

## Implementation Steps

1. Review baseline screenshots and proposal images for targeted forms.
2. Change one module form group at a time.
3. Prefer `Tabs`, `FormSection`, `Grid`, `FormRow`, and existing module CSS.
4. Capture post-change screenshots and compare against proposals.
5. Update checklist status before moving to the next targeted form.

## Scope Exceptions

- Sandbox-only controls are not required to receive targeted page-specific edits unless they reveal a shared component regression.

## Do Not Do

- Do not change persisted data models.
- Do not silently hide validation errors or save failures.
- Do not add a new CSS framework or one-off stringly identifiers.

## Acceptance Checklist

- [x] Targeted forms have clearer grouping and width behavior.
- [x] No implemented form is missing proposal and validation screenshots.
- [x] No page shows broken layout at desktop or narrow validation widths.

## Proof Required

- Code diff: shared and module form files in the working tree.
- Build result: `dotnet build CanDoItAll.slnx` passed with 0 warnings and 0 errors.
- Browser screenshot paths and comparison notes: `C:\repositories\CanDoItAll\.codex\bundles\form-layout-usability\reviews\01-execution-report.md`.
- Updated workbook rows: `C:\repositories\CanDoItAll\output\form-layout-usability\form-layout-checklist.xlsx`.

## Browser Validation Logging

- Record route, viewport, actions, screenshot path, and pass/fail result for each changed form region.
- Include comparison notes against the corresponding image proposal.

## Progression Gate

- Pass only when all targeted changed forms are validated or explicitly blocked with a follow-up row.
- Reopen subbundle 01 when a repeated width or textarea issue belongs in shared CSS.

## Suggested Agent Prompt

Implement targeted form layout changes for high-density product modules after shared foundation proof passes. Keep edits minimal, compare each changed form against its proposal image, and update the workbook and execution report as proof is captured.
