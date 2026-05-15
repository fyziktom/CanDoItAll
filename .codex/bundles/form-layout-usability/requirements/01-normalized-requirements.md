# Normalized Requirements

| ID | Requirement | Observable Success Criteria |
| --- | --- | --- |
| FRM-001 | Inventory all editable value-entry form surfaces across the app and classify them by shared component, route, density, textarea usage, and risk. | `inventories/01-scope-inventory.md` and the `.xlsx` checklist include all files found by the form scan or explicitly mark a scope exception. |
| FRM-002 | Shared form fields must stretch predictably to available width. | BaseLib `FormField` content wrappers prevent shrink/overflow and real form screenshots show inputs using their parent width. |
| FRM-003 | Textareas for larger text must have readable default sizes. | BaseLib `TextArea` and `.cda-input--textarea` provide a larger default/minimum without breaking explicit larger sizes. |
| FRM-004 | Dense forms must be grouped into topical sections or subtabs where one section becomes hard to scan. | Process and selected CRM/agent editors show topical grouping; screenshots show fewer unrelated fields in one vertical run. |
| FRM-005 | Forms should look enterprise-ready without decorative noise. | Shared sections expose a compact icon/kicker affordance or equivalent existing component styling, using the app icon system and restrained colors. |
| FRM-006 | Every form-only screenshot used for analysis must have an imagegen proposal. | Proposal image path recorded for each screenshot row in the `.xlsx` checklist. |
| FRM-007 | Every implemented change must be validated by browser screenshot and compared against the proposal. | Checklist row status is `Validated` only when post-change screenshot path and comparison result are present. |
| FRM-008 | Keep edits minimal and compatible with existing Blazor/Radzen components. | No new UI framework, no broad rewrite of business logic, and build succeeds. |
