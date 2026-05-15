# Form Layout Usability

## Profile

- `initiative`

## Mission

Improve editable form surfaces so fields use the available horizontal space, long-text fields have readable default sizes, dense editors are split by topic where needed, and the app presents form work as a polished enterprise workflow rather than a compressed set of inputs.

## Outcome Contract

- Requested outcome: all value-entry forms in the Blazor app are inventoried, visually reviewed, and improved through the smallest shared or targeted changes that make field sizing, textarea readability, grouping, and action affordances predictable.
- Hard constraints: use existing BaseLib/Radzen-backed form components where present; improve shared wrappers before adding one-off structural CSS; preserve strongly typed C#; do not add XML documentation comments; generated image proposals are planning artifacts only and cannot replace browser proof.
- Evidence required before closure: source inventory, `.xlsx` checklist, generated form-only proposal images, implementation diffs, build/test output, browser screenshots before and after for representative form regions, and checklist rows tying each changed form to proposal and validation screenshots.
- Known blockers or explicit scope exceptions: the CanDoItAll Components MCP returned `Transport closed`, so component inventory must be grounded in local source and existing BaseLib usage; sandbox-only Space3D/WebGL forms are reviewed but lower priority than shipped `CanDoItAll.Web` module routes.

## Bundle Layout

- `inputs/` raw request, artifacts, and structured input
- `analysis/` current state, assumptions, and risks
- `requirements/` normalized, testable requirements
- `architecture/` target solution and important boundaries
- `plan/` execution order and dependencies
- `traceability/` requirement-to-bundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` bundle self-review and execution report
- `inventories/` form inventory and scope summary
- `evidence/` screenshots, proposals, and comparison notes created during execution

## Recommended Execution Order

1. `subbundles/01-01-shared-form-foundation`
2. `subbundles/02-02-module-form-layouts`
3. `subbundles/03-03-validation-checklist-and-proof`

## Dependency And Validation Map

- Shared form sizing is a critical foundation. Do not close module-specific layout work until shared textarea and field-stretch behavior is proven in the component sandbox and at least one real module form.
- Dense module editors depend on the foundation because many targeted forms use `FormField`, `FormSection`, `Grid`, and `cda-input`.
- Final closure depends on screenshot comparison rows in the workbook and `reviews/01-execution-report.md`.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Completed`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed`
- Browser validation analytics: `Captured`
- Checklist workbook: `C:\repositories\CanDoItAll\output\form-layout-usability\form-layout-checklist.xlsx`
- Build proof: `npm --prefix Tailwind run build` passed; `dotnet build CanDoItAll.slnx` passed with 0 warnings and 0 errors.
- Browser proof: post-change form screenshots are stored in `C:\repositories\CanDoItAll\output\form-layout-usability\screenshots`.
