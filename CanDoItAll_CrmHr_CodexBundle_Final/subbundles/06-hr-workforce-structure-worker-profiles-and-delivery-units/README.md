# B06 - HR workforce structure, worker profiles, and delivery units

## Status

- `Completed`

## Objective

- Add workforce profiles for employees, contractors, freelancers, and delivery units, including reporting lines, home units, lifecycle dates, rates, seniority, and structure-aware views.

## Covered Inputs

- Original request path: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\inputs\00-original-request.md`
- Legacy subbundle package: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B06_hr_workforce_structure_profiles_and_delivery_units`
- Story IDs: HR-01, HR-02, HR-03, HR-04, HR-05, HR-06, HR-16, HR-17, HR-29, HR-30, HR-34, HR-36, DIR-16

## Prerequisites

- `B01` must be completed or honestly blocked before this subbundle starts.
- `B02` must be completed or honestly blocked before this subbundle starts.
- `B03` must be completed or honestly blocked before this subbundle starts.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B06_hr_workforce_structure_profiles_and_delivery_units\README.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B06_hr_workforce_structure_profiles_and_delivery_units\FILE_REFERENCES.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B06_hr_workforce_structure_profiles_and_delivery_units\ACCEPTANCE_CRITERIA.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B06_hr_workforce_structure_profiles_and_delivery_units\IMPLEMENTATION_PROMPT.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B06_hr_workforce_structure_profiles_and_delivery_units\VALIDATION_PROMPT.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs`

## Deliverables

- Ship the concrete outcome described by `B06` across route scope `/crm-hr/workforce`.
- Preserve and update the detailed legacy docs under `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B06_hr_workforce_structure_profiles_and_delivery_units` as execution evidence when implementation reality changes scope or proof.
- Update tests, browser evidence, and bundle reporting required by this phase.

## Dependency Impact

- Prerequisite set: `B01, B02, B03`.
- Downstream dependents: `B07, B08, B10, B11, B12, B13`.
- Weak proof here must reopen this subbundle before dependent work continues.

## Validation Depth

- Critical domain integration

## Implementation Steps

1. Reopen the preserved architect docs and the live repo source references listed above.
2. Re-run the entry gate against current code before editing feature files.
3. Implement only the smallest correct change set for this subbundle and its owned stories.
4. Run the proof required for this phase and update the execution report while the evidence is fresh.
5. Run the closure gate and reopen the subbundle immediately if proof is weak or contradicted by later behavior.

## Scope Exceptions

- None pre-approved. If current repo contracts force a scope change, repair the bundle before calling the phase complete.

## Do Not Do

- Do not import CanvasLib into CRM/HR pages.
- Do not bypass current storage-placement, search, activity, or project-structure service boundaries.
- Do not replace project-local participant behavior with a forced central-directory-only model.

## Acceptance Checklist

- A person can have a workforce profile without losing CRM identity continuity.
- A delivery unit can be represented as a party with workforce semantics.
- Workforce detail shows home unit and manager relationships clearly.
- Component and Playwright tests prove profile editing.

## Proof Required

- Run a solution build or the smallest build slice that proves all touched contracts still compile.
- Run the smallest relevant unit, component, integration, or Playwright suites introduced or affected by this phase.
- Capture large-screen screenshots, inspect them, then repeat narrower-width validation when layout changed.

## Browser Validation Logging

- Target routes: `/crm-hr/workforce`.
- Required viewports: `1600x1000` first, then narrower widths on the same page context when layout changed.
- Required Playwright evidence: navigate, perform route-specific actions, assert expected UI state, and capture screenshots.
- Expected screenshot folder: `C:\repositories\CanDoItAll\evidence\crm-hr\b06\`.
- Screenshot review questions must answer readability, overlap, clipping, hierarchy, and alignment before closure.

## Progression Gate

- Downstream subbundles `B07, B08, B10, B11, B12, B13` may continue only after this phase records trusted build/test evidence and the required gate row is updated.

## Suggested Agent Prompt

```text
Implement B06 only. Start with the workflow README in this folder, then reconcile the preserved architect package at C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B06_hr_workforce_structure_profiles_and_delivery_units against the live repo files listed under Exact Source References before editing code.
```

