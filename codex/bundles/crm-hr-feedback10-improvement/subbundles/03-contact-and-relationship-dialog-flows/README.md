# SB03 Contact And Relationship Dialog Flows

## Status

- `Completed`

## Objective

- Eliminate the mutable-loop-index crash family and deliver a two-step, isolated-draft add-contact wizard with persisted contact tags and scalable relationship selection.

## Success Criteria

- Contact/address/relationship callbacks target stable rows after render/reorder; add-empty-remove/cancel never throws.
- Add Contact opens type-card step then validated fields/tag step; Back preserves, Cancel discards, Finish adds once.
- Contact-specific tags persist through entity/configuration/migration/save/load/clone/import-export/merge paths.
- Relationship selection uses SB01/SB02 picker.

## Covered Inputs

- `N003`; `R003`, `R004`, `R013`, `R014`.

## Prerequisites

- SB02 and `CP-02` passed.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/PartyContactMethodsEditor.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/PartyAddressesEditor.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Components/PartyRelationshipsEditor.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Pages/CrmHrDirectoryPage.razor`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Models/CrmHrFoundationModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/PartyDirectoryManagementService.cs`
- `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/PartyRelationshipsEditorTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/CrmHrSchemaIntegrationTests.cs`

## UI Composition Contract

- Primary surface: existing contact list; Add opens medium dialog.
- Supporting content: step indicator, validation, concise help; no stats.
- List/editor organization: two-step dialog; existing contacts remain visible behind overlay; relationship picker wide.
- Textarea/dialog sizing: standard notes textarea; medium contact dialog with stable footer.
- First viewport: type cards or all core fields plus footer visible at `1800x1100`.
- Scroll owner: dialog body if needed; header/footer remain visible.
- Motion: restrained entrance/focus and step transition only.

## Deliverables

- Stable callback identity fixes for contact/address/relationship editors.
- Contact wizard with isolated typed draft and TagEditor.
- Contact tag persistence/migration compatibility.
- Relationship picker integration and Behavioral proof.

## Dependency Impact

- SB04 reuses safe dialog draft conventions; failures block all later work.

## Validation Depth

- Proof tier: `Behavioral`.
- Architecture checkpoint: `CP-03`.

## C# Architecture Impact

- Adds persisted contact tags and extracts dialog state from Directory page.

## Boundary Ownership

- Dialog owns draft/steps; page commits result; persistence types/services own tags.

## Dependency Direction

- Existing CRM/HR-to-Infrastructure/migration direction only; no UI type in persistence.

## Pattern Decision

- Small enum/switch wizard state; stable item identity, not captured mutable index.

## Testability Contract

- Step/state and callback tests run without page/database; round-trip/migration tests use normal persistence fixtures.

## Partial Class Policy

- No Directory/service feature partial; use top-level component/state types.

## Architecture Proof Required

- Source assertions for all three closure sites, direct wizard tests, migration/round-trip proof, no-new-partial, page-orchestration proof, solution build.

## Implementation Steps

1. Add failing regression tests for one-row and reordered contact/address/relationship callbacks.
2. Replace mutable index capture with stable row identity/captured row index.
3. Implement two-step contact wizard and isolated draft validation.
4. Add contact `TagsJson` mapping/default/migration and compatibility-path updates.
5. Integrate relationship picker; add component/integration tests.
6. Run Directory Playwright proof and `CP-03`.

## Scope Exceptions

- Primary email/phone remain the existing primary-contact fields; wizard creates additional contacts and does not redefine primary semantics.

## Do Not Do

- Do not merely bounds-check `RemoveAt`, mutate live collection before Finish, store tags only in UI state, or skip migration compatibility.

## Acceptance Checklist

- [x] Exact reported sequence cannot throw.
- [x] Adjacent callback-capture defects are fixed.
- [x] Wizard transitions/cancel/finish are deterministic.
- [x] Contact tags normalize and round-trip.
- [x] Relationship picker is scalable.

## Execution Evidence

- Shipped behavior: contact, address, and relationship callbacks target the rendered row identity; Add Contact owns an isolated two-step draft with type selection, validation, tags, Back, Cancel, and Finish; relationships use the paged party picker.
- Semantic positive proof: `repo://tests/Components/CanDoItAll.Tests.Components/PartyContactMethodsEditorTests.cs` proves valid Finish adds exactly one contact and Back preserves the isolated draft; `PartyRelationshipsEditorTests.cs` proves typed picker selection commits the selected id.
- Adversarial negative proof: the contact/address/relationship component tests cover one-row removal, reorder-then-remove/direction update, invalid Finish, picker cancel, and cancel after entered draft data without mutating the caller.
- Persistence proof: `repo://tests/Integration/CanDoItAll.Tests.Integration/CrmHrSchemaIntegrationTests.cs` and `PartyDirectoryIntegrityIntegrationTests.cs` cover aggregate round trip, tag preservation, relationship invariants, atomic import failure, and legacy migration behavior.
- Migration proof: `repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260724114400_ImproveCrmHrRecordSelectionAndRecognitionIntegrity.cs`; final EF model drift was empty.
- Browser proof: `repo://output/playwright/crm-hr-feedback10/directory-add-contact-dialog-1800x1100.png`.
- Progression decision: `CP-03 passed`; opportunity dialogs reused the isolated-draft convention.

## Proof Required

- Raw note owned: literal `N003`.
- Shipped/source/test proof includes closure root cause and migration.
- Shallow-pass trap: bounds-checking the wrong index or hiding the row without fixing identity.
- Adversarial negative proof: one-row remove, reorder then remove/direction update, invalid Finish, Cancel after entered tags.
- Semantic positive proof: create tagged contact, save/reload, then select a later-page relationship.
- Anti-stub audit: no UI-only tag storage, TODO migration, swallowed exception, or fixture branch.

## Browser Validation Logging

- Route: `/crm-hr/directory?partyId=<seeded-party>`.
- Viewport: `1800x1100`.
- Actions: open wizard, type step, next/back, invalid validation, tags, cancel, finish, remove; open relationship picker.
- Screenshots: `bundle://evidence/browser/SB03/contact-type-step.png`, `bundle://evidence/browser/SB03/contact-fields-step.png`, `bundle://evidence/browser/SB03/relationship-picker.png`.
- Review: focus, centered type cards, validation, footer actions, clipping, scroll, unchanged list on cancel.

## Progression Gate

- SB04 starts only after `CP-03`, migration round-trip, and the exact crash regression pass.

## Reopen Triggers

- Reopen for wrong-row mutation, leaked draft, tag loss, migration incompatibility, picker bypass, dialog clipping, or regression exception.

## Suggested Agent Prompt

```text
Implement SB03 only. Reproduce and fix the callback-index defect family, add the isolated two-step tagged contact wizard and migration-safe persistence, use the shared relationship picker, capture Behavioral/browser proof, update CP-03/report, and stop if the exact crash path or round trip does not pass.
```
