# 01 Shared Form Foundation

## Status

- Subbundle status: `Completed`

## Objective

Make shared form wrappers and textarea styles stretch and read well by default, so repeated module forms improve with minimal targeted edits.

## Covered Inputs

- Form stretching complaint.
- Textarea default-size complaint.
- Enterprise clarity/aesthetics request where shared section cues can improve scanning.

## Prerequisites

- Bundle readiness gate passed.
- Source references below exist.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\FormField.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\TextArea.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\FormSection.razor`
- `C:\repositories\CanDoItAll\Tailwind\forms\fields.css`
- `C:\repositories\CanDoItAll\Tailwind\input.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\wwwroot\css\output.css`

## Deliverables

- Shared field content wrapper has explicit full-width/min-width behavior.
- Shared textarea default rows and CSS defaults are readable.
- Shared form sections gain a restrained icon/kicker affordance without requiring all callers to change.

## Dependency Impact

- Critical foundation for module form screenshots.
- If this subbundle is wrong, later module proof must be reopened.

## Validation Depth

- `npm --prefix Tailwind run build`
- `dotnet build CanDoItAll.slnx`
- Browser screenshot of `/groups/inputs`.
- Browser screenshot of at least one product form using `cda-input--textarea`.

## Implementation Steps

1. Update `FormField` child wrapper classes.
2. Increase `TextArea.Rows` default.
3. Update `.cda-input--textarea` shared CSS with width, resize, line height, and minimum height.
4. Add optional `Icon` parameter and compact default visual treatment to `FormSection`.
5. Rebuild BaseLib output CSS.

## Scope Exceptions

- Do not alter form model binding, validation, or save behavior.

## Do Not Do

- Do not replace BaseLib forms with a new form framework.
- Do not add XML documentation comments.
- Do not use decorative gradients or oversized cards for routine form UI.

## Acceptance Checklist

- [x] Shared controls compile.
- [x] Existing form callers remain source-compatible.
- [x] Textareas with explicit larger module CSS are not made smaller.
- [x] Browser screenshots show full-width fields and readable multiline defaults.

## Proof Required

- Build output: `dotnet build CanDoItAll.slnx` passed with 0 warnings and 0 errors.
- CSS output: `npm --prefix Tailwind run build` passed.
- Before/after screenshot rows in workbook: `C:\repositories\CanDoItAll\output\form-layout-usability\form-layout-checklist.xlsx`.
- Execution report browser analytics row: `C:\repositories\CanDoItAll\.codex\bundles\form-layout-usability\reviews\01-execution-report.md`.

## Browser Validation Logging

- Record route, viewport, actions, screenshot path, and pass/fail result in `reviews/01-execution-report.md`.
- Include at least one sandbox form and one product form.

## Progression Gate

- Pass only if shared foundation proof works in both sandbox and one real product form.
- Block downstream module layout work if shared textarea defaults or field stretching fail.

## Suggested Agent Prompt

Implement the shared BaseLib form foundation updates using the smallest compatible change set, rebuild Tailwind output, run build, and capture browser proof for the shared inputs sandbox and one product form.
