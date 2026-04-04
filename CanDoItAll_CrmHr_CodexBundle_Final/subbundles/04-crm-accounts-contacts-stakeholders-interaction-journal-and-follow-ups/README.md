# B04 - CRM accounts, contacts, stakeholders, interaction journal, and follow-ups

## Status

- `Completed`

## Objective

- Implement account and contact views, stakeholder role handling, interaction logging, account summaries, and overdue next-action workflows on top of the unified party model.

## Covered Inputs

- Original request path: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\inputs\00-original-request.md`
- Legacy subbundle package: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B04_crm_accounts_contacts_interactions_and_followups`
- Story IDs: CRM-01, CRM-02, CRM-03, CRM-04, CRM-05, CRM-11, CRM-12, CRM-14, CRM-17, CRM-20, CRM-21, CRM-22, CRM-23, DIR-14

## Prerequisites

- `B01` must be completed or honestly blocked before this subbundle starts.
- `B02` must be completed or honestly blocked before this subbundle starts.
- `B03` must be completed or honestly blocked before this subbundle starts.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B04_crm_accounts_contacts_interactions_and_followups\README.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B04_crm_accounts_contacts_interactions_and_followups\FILE_REFERENCES.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B04_crm_accounts_contacts_interactions_and_followups\ACCEPTANCE_CRITERIA.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B04_crm_accounts_contacts_interactions_and_followups\IMPLEMENTATION_PROMPT.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B04_crm_accounts_contacts_interactions_and_followups\VALIDATION_PROMPT.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity\ActivityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Search\SearchIndexing.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`

## Deliverables

- Ship the concrete outcome described by `B04` across route scope `/crm-hr/crm, /crm-hr/directory`.
- Preserve and update the detailed legacy docs under `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B04_crm_accounts_contacts_interactions_and_followups` as execution evidence when implementation reality changes scope or proof.
- Update tests, browser evidence, and bundle reporting required by this phase.

## Dependency Impact

- Prerequisite set: `B01, B02, B03`.
- Downstream dependents: `B05, B11, B12, B13`.
- Weak proof here must reopen this subbundle before dependent work continues.

## Validation Depth

- UI, component-test, and browser-proof

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

- Accounts and contacts can be reviewed from CRM routes.
- Interactions persist with participants and next-action ownership.
- Overdue next actions are visible.
- Account detail shows stakeholders and recent interaction history.

## Proof Required

- Run a solution build or the smallest build slice that proves all touched contracts still compile.
- Run the smallest relevant unit, component, integration, or Playwright suites introduced or affected by this phase.
- Capture large-screen screenshots, inspect them, then repeat narrower-width validation when layout changed.

## Browser Validation Logging

- Target routes: `/crm-hr/crm, /crm-hr/directory`.
- Required viewports: `1600x1000` first, then narrower widths on the same page context when layout changed.
- Required Playwright evidence: navigate, perform route-specific actions, assert expected UI state, and capture screenshots.
- Expected screenshot folder: `C:\repositories\CanDoItAll\evidence\crm-hr\b04\`.
- Screenshot review questions must answer readability, overlap, clipping, hierarchy, and alignment before closure.

## Progression Gate

- Downstream subbundles `B05, B11, B12, B13` may continue only after this phase records trusted build/test evidence and the required gate row is updated.

## Suggested Agent Prompt

```text
Implement B04 only. Start with the workflow README in this folder, then reconcile the preserved architect package at C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B04_crm_accounts_contacts_interactions_and_followups against the live repo files listed under Exact Source References before editing code.
```

