# B03 - Contact points, addresses, relationships, org structure, import/export, and duplicate merge

## Status

- `Completed`

## Objective

- Finish the party directory by implementing contact methods, addresses, role assignments, relationship editors, import/export flows, and a safe duplicate merge experience.

## Covered Inputs

- Original request path: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\inputs\00-original-request.md`
- Legacy subbundle package: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B03_contact_points_addresses_relationships_and_dedup`
- Story IDs: DIR-06, DIR-07, DIR-08, DIR-09, DIR-10, DIR-11, DIR-12, DIR-13, HR-29, HR-30, X-12

## Prerequisites

- `B01` must be completed or honestly blocked before this subbundle starts.
- `B02` must be completed or honestly blocked before this subbundle starts.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B03_contact_points_addresses_relationships_and_dedup\README.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B03_contact_points_addresses_relationships_and_dedup\FILE_REFERENCES.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B03_contact_points_addresses_relationships_and_dedup\ACCEPTANCE_CRITERIA.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B03_contact_points_addresses_relationships_and_dedup\IMPLEMENTATION_PROMPT.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B03_contact_points_addresses_relationships_and_dedup\VALIDATION_PROMPT.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchMetadata.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Search\SearchIndexing.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity\ActivityModels.cs`

## Deliverables

- Ship the concrete outcome described by `B03` across route scope `/crm-hr/directory`.
- Preserve and update the detailed legacy docs under `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B03_contact_points_addresses_relationships_and_dedup` as execution evidence when implementation reality changes scope or proof.
- Update tests, browser evidence, and bundle reporting required by this phase.

## Dependency Impact

- Prerequisite set: `B01, B02`.
- Downstream dependents: `B04, B05, B06, B07, B08, B09, B10, B11, B12, B13`.
- Weak proof here must reopen this subbundle before dependent work continues.

## Validation Depth

- Critical directory data foundation

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

- A party can hold multiple contact methods and addresses.
- Parent-child and reporting relationships can be created and edited.
- Duplicate merge preserves related history instead of orphaning it.
- Import/export flows are available and validated in browser evidence.

## Proof Required

- Run a solution build or the smallest build slice that proves all touched contracts still compile.
- Run the smallest relevant unit, component, integration, or Playwright suites introduced or affected by this phase.
- Capture large-screen screenshots, inspect them, then repeat narrower-width validation when layout changed.

## Browser Validation Logging

- Target routes: `/crm-hr/directory`.
- Required viewports: `1600x1000` first, then narrower widths on the same page context when layout changed.
- Required Playwright evidence: navigate, perform route-specific actions, assert expected UI state, and capture screenshots.
- Expected screenshot folder: `C:\repositories\CanDoItAll\evidence\crm-hr\b03\`.
- Screenshot review questions must answer readability, overlap, clipping, hierarchy, and alignment before closure.

## Progression Gate

- Downstream subbundles `B04, B05, B06, B07, B08, B09, B10, B11, B12, B13` may continue only after this phase records trusted build/test evidence and the required gate row is updated.
- Because this is a critical foundation, at least one dependent-flow smoke must pass before downstream work may continue.

## Suggested Agent Prompt

```text
Implement B03 only. Start with the workflow README in this folder, then reconcile the preserved architect package at C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B03_contact_points_addresses_relationships_and_dedup against the live repo files listed under Exact Source References before editing code.
```

