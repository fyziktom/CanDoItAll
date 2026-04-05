# B01 - Foundation: unified party domain, schema, and module skeleton

## Status

- `Completed`

## Objective

- Create the new CRM/HR module project, full relational schema, seed strategy, service registration, startup wiring, and core DTOs around a unified Party model that can represent persons, organizations, organization units, and AI agents.

## Covered Inputs

- Original request path: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\inputs\00-original-request.md`
- Legacy subbundle package: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B01_foundation_unified_party_domain`
- Story IDs: DIR-01, DIR-02, DIR-04, DIR-05, DIR-16, DIR-17, DIR-18, DIR-19, DIR-20, X-05, X-08, X-09, X-14

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B01_foundation_unified_party_domain\README.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B01_foundation_unified_party_domain\FILE_REFERENCES.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B01_foundation_unified_party_domain\ACCEPTANCE_CRITERIA.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B01_foundation_unified_party_domain\IMPLEMENTATION_PROMPT.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B01_foundation_unified_party_domain\VALIDATION_PROMPT.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Composition\ModuleAssemblies.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\AppDbContext.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Resources\ResourceModels.cs`

## Deliverables

- Ship the concrete outcome described by `B01` across route scope `no direct route`.
- Preserve and update the detailed legacy docs under `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B01_foundation_unified_party_domain` as execution evidence when implementation reality changes scope or proof.
- Update tests, browser evidence, and bundle reporting required by this phase.

## Dependency Impact

- Prerequisite set: `none`.
- Downstream dependents: `B02, B03, B04, B05, B06, B07, B08, B09, B10, B11, B12, B13`.
- Weak proof here must reopen this subbundle before dependent work continues.

## Validation Depth

- Critical foundation

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

- Fresh app startup creates the CRM/HR tables without manual intervention.
- The solution builds after module registration changes.
- Integration tests prove schema creation and at least one round-trip save/load for the Party aggregate.
- No existing module startup path is broken by the new module registration.

## Proof Required

- Run a solution build or the smallest build slice that proves all touched contracts still compile.
- Run the smallest relevant unit, component, integration, or Playwright suites introduced or affected by this phase.
- Capture dependent-surface or host proof when this non-UI foundation unlocks later UI or integration work.

## Browser Validation Logging

- N/A for direct route coverage in this phase.
- If this foundation changes startup or shared UI contracts, record the dependent browser smoke used to prove downstream safety.

## Progression Gate

- Downstream subbundles `B02, B03, B04, B05, B06, B07, B08, B09, B10, B11, B12, B13` may continue only after this phase records trusted build/test evidence and the required gate row is updated.
- Because this is a critical foundation, at least one dependent-flow smoke must pass before downstream work may continue.

## Suggested Agent Prompt

```text
Implement B01 only. Start with the workflow README in this folder, then reconcile the preserved architect package at C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B01_foundation_unified_party_domain against the live repo files listed under Exact Source References before editing code.
```

