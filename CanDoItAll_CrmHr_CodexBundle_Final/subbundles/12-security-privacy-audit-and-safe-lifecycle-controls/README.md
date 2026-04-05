# B12 - Security, privacy, audit, and safe lifecycle controls

## Status

- `Completed`

## Objective

- Add sensitive-data markers, audit entries, soft-delete rules, safe search behavior, HR-only note separation, and future-ready permission seams suitable for the current local-user model.

## Covered Inputs

- Original request path: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\inputs\00-original-request.md`
- Legacy subbundle package: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B12_security_privacy_audit_and_safe_lifecycle_controls`
- Story IDs: DIR-18, DIR-19, HR-28, X-09, X-10, X-11

## Prerequisites

- `B01` must be completed or honestly blocked before this subbundle starts.
- `B02` must be completed or honestly blocked before this subbundle starts.
- `B03` must be completed or honestly blocked before this subbundle starts.
- `B04` must be completed or honestly blocked before this subbundle starts.
- `B06` must be completed or honestly blocked before this subbundle starts.
- `B08` must be completed or honestly blocked before this subbundle starts.
- `B11` must be completed or honestly blocked before this subbundle starts.

## Exact Source References

- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B12_security_privacy_audit_and_safe_lifecycle_controls\README.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B12_security_privacy_audit_and_safe_lifecycle_controls\FILE_REFERENCES.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B12_security_privacy_audit_and_safe_lifecycle_controls\ACCEPTANCE_CRITERIA.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B12_security_privacy_audit_and_safe_lifecycle_controls\IMPLEMENTATION_PROMPT.md`
- `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B12_security_privacy_audit_and_safe_lifecycle_controls\VALIDATION_PROMPT.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Security\SecurityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity\ActivityModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Search\SearchIndexing.cs`

## Deliverables

- Ship the concrete outcome described by `B12` across route scope `/crm-hr, /crm-hr/directory, /crm-hr/workforce`.
- Preserve and update the detailed legacy docs under `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B12_security_privacy_audit_and_safe_lifecycle_controls` as execution evidence when implementation reality changes scope or proof.
- Update tests, browser evidence, and bundle reporting required by this phase.

## Dependency Impact

- Prerequisite set: `B01, B02, B03, B04, B06, B08, B11`.
- Downstream dependents: `B13`.
- Weak proof here must reopen this subbundle before dependent work continues.

## Validation Depth

- Process-critical privacy closure

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

- Confidential notes are stored and displayed separately from broad operational notes.
- Sensitive content is not indexed into global search.
- Audit trail entries exist for important lifecycle and data changes.
- Archive/reactivate flows preserve history.

## Proof Required

- Run a solution build or the smallest build slice that proves all touched contracts still compile.
- Run the smallest relevant unit, component, integration, or Playwright suites introduced or affected by this phase.
- Capture large-screen screenshots, inspect them, then repeat narrower-width validation when layout changed.

## Browser Validation Logging

- Target routes: `/crm-hr, /crm-hr/directory, /crm-hr/workforce`.
- Required viewports: `1600x1000` first, then narrower widths on the same page context when layout changed.
- Required Playwright evidence: navigate, perform route-specific actions, assert expected UI state, and capture screenshots.
- Expected screenshot folder: `C:\repositories\CanDoItAll\evidence\crm-hr\b12\`.
- Screenshot review questions must answer readability, overlap, clipping, hierarchy, and alignment before closure.

## Progression Gate

- Downstream subbundles `B13` may continue only after this phase records trusted build/test evidence and the required gate row is updated.
- Because this is a critical foundation, at least one dependent-flow smoke must pass before downstream work may continue.

## Execution Notes

- Reused the live privacy and audit foundations that already existed in the repo instead of adding a parallel security layer. `Party.IsSensitive`, `PartyConfidentialNote`, `CrmHrAuditEntry`, current search suppression, and the shared timeline components stayed authoritative; B12 added the missing user-facing privacy surfaces and tightened the current behavior.
- Added a shared `SensitiveDataCallout` and wired it into `/crm-hr/directory` and `/crm-hr/workforce`, added a privacy-posture summary card on `/crm-hr`, separated confidential notes from operational notes on the directory page, and surfaced audit/history context on workforce by reusing the shipped timeline infrastructure instead of introducing a second audit panel.
- Repaired a stale repo bug while closing the phase: `GetWorkforceWorkspaceAsync` now returns the real party `LastChangedBy` and `UpdatedAtUtc` values, and `GetPartyAsync` now orders confidential notes client-side so SQLite and PostgreSQL both support the same behavior.
- Closure also repaired a proof mismatch in the preserved bundle. The browser test seeds a sensitive party directly into the Playwright fixture database so the UI proof stays focused on visible privacy markers, archive/reactivate behavior, home-route posture, and workforce audit history while component and integration tests cover confidential-note editing and persistence boundaries.

## Proof Captured

- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx -nologo -v minimal`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj -nologo -v minimal --filter FullyQualifiedName~CrmHrPrivacyBoundaryTests`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj -nologo -v minimal --filter FullyQualifiedName~CrmHrAuditTrailIntegrationTests`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj -nologo -v minimal --filter FullyQualifiedName~CrmHrSensitiveDataFlowTests`
- Browser artifacts: `C:\repositories\CanDoItAll\evidence\crm-hr\b12\crm-hr-directory-b12-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b12\crm-hr-home-b12-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b12\crm-hr-workforce-b12-desktop.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b12\crm-hr-workforce-b12-tablet.png`, `C:\repositories\CanDoItAll\evidence\crm-hr\b12\screenshot-review.md`

## Suggested Agent Prompt

```text
Implement B12 only. Start with the workflow README in this folder, then reconcile the preserved architect package at C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final\07_ITEMS\B12_security_privacy_audit_and_safe_lifecycle_controls against the live repo files listed under Exact Source References before editing code.
```

