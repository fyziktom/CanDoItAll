# SB08 — Central publication and client source/import desktop UI

State: `LOCKED`  
Proof tier: `Behavioral`  
Depends on: `SB07`  
Next on pass: `SB09`

## Objective

Implement a desktop provider-management experience for publishing local profiles, managing sources, discovering/selecting imports, and understanding shared ownership/availability.

## Observable outcome

The central and client user journeys are complete without exposing secrets or allowing remote-owned fields to be edited.

## Inputs and current-state anchors

- Bundle root execution contract and architecture documents.
- Current repository state, not only the prepared SHA.
- Relevant source/test impact maps.
- Completed proof and handoff from every dependency.
- Current mandatory SharedInfo skills.

## Scope

- Read current candoitall-components-mcp compact UI guidance and record first viewport/scroll owner.
- Extend provider list summaries with Local/Shared origin, source, publication and availability badges.
- Add local profile sharing section with eligibility reasons and publish/unpublish actions.
- Add source list and add/edit/test/enable/disable workflow.
- Add catalog refresh and multi-select import dialog.
- Add imported profile view with local alias/enabled editable and remote-owned fields read-only.
- Add retire/remove confirmation respecting reference policy.
- Render loading/empty/success/unauthorized/offline/stale/unpublished/identity-mismatch/conflict states.
- Extract cohesive child components and presentation models instead of growing one code-behind.
- Use Workspace services only; no HttpClient/EF/secret value access in components.
- Add focused component tests; Playwright remains SB09.

## Out of scope

- No mobile-specific app layout.
- No new backend contract unless a proven UI-blocking defect is reopened through its owner.
- No provider token display.
- No public administration API.

## Implementation sequence

1. Inspect existing ProviderManagementPanel composition and current shared BaseLib components.
2. Document compact desktop composition before markup.
3. Keep one deliberate page/panel vertical scroll owner.
4. Use bounded catalog dialog list scroll only.
5. Keep primary add/source/sync actions in first viewport.
6. Map application result/error codes to presentation states.
7. Disable or omit remote-owned inputs server-side and client-side.
8. Do not render secret values after save/test.
9. Preserve existing local provider editor behavior and pricing tabs.
10. Add bUnit/component tests for state transitions and service commands.

## C# Architecture Impact

This subbundle is architecture-significant. Re-read
`architecture/00-csharp-current-state-inventory.md` through
`architecture/04-csharp-testability-plan.md`, update the affected checkpoint, and stop rather
than use a boundary workaround.

## Boundary Ownership

Workspace Razor owns presentation. Workspace application services own mutations. BaseLib owns reusable primitives.

## Dependency Direction

UI references module application models/services only. No new Integration Http or EF reference from component.

Record before and after `ProjectReference`/namespace direction even when no reference is
expected to change. A no-change result is still evidence.

## Pattern Decision

Container/presentational components, explicit view state, command handlers, modal/dialog workflow.

Do not introduce an adjacent alternative pattern without reopening the owning ADR and
recording why the selected pattern failed.

## Testability Contract

Mock/fake application service at component boundary; stable selectors/ARIA labels for Playwright.

Every new behavior needs one realistic positive proof and one meaningful negative proof. Test
existence, file counts, status codes alone, or mocked self-assertions do not prove behavior.

## Partial Class Policy

Extract child components and code-behind classes. Do not append all source/import state to the existing large component.

A large partial or monolithic file is a gate failure unless the architecture review documents
a narrow unavoidable reason.

## Architecture Proof Required

- Compact composition decision.
- Component tree and ownership map.
- Focused component tests.
- No direct HttpClient/DbContext guardrail.
- Existing local provider regression.
- No secret rendering snapshot.

## Test selection

| Topic | Owning project/lane | Stable filter | Planned expected discovery | Selection reason |
| --- | --- | --- | ---: | --- |
| `SharedProviderPublicationPanelTests` | `tests/Solutions/CanDoItAll.Tests.Components.slnx` | `FullyQualifiedName~SharedProviderPublicationPanelTests` | 10 | Covers central eligibility and publish/unpublish presentation. |
| `SharedProviderSourceAndImportComponentTests` | `tests/Solutions/CanDoItAll.Tests.Components.slnx` | `FullyQualifiedName~SharedProviderSourceAndImportComponentTests` | 18 | Covers source CRUD/test/catalog/import/read-only/error states. |

Before running a test topic:

1. build the owning production/test assembly;
2. run `--list-tests` when it is a .NET test lane;
3. compare actual discovery with the planned count;
4. update the planned count only before execution and with a written implementation-based
   reason;
5. reject zero discovery;
6. record transcript and counts in `proof/proof-manifest.json`.

Do not run an unfiltered project or broader lane unless this subbundle explicitly owns it.

## Acceptance criteria

- Central publish state and ineligibility are understandable.
- Source can be added/tested and catalog selected.
- Imported ownership/read-only state is clear.
- Personal and shared profiles coexist visually.
- All required error/empty/loading states render.
- Desktop first viewport/scroll owner decision is implemented.

## Negative proof

- Remote-owned field mutation command is not emitted and service rejects a forged mutation.
- Secret value is absent from rendered markup.
- Unavailable source cannot be presented as healthy.
- Existing local provider create/edit/delete remains functional.

## Semantic invariants

- UI never owns HTTP/persistence/security behavior.
- Remote-owned fields remain immutable through service boundary.
- No secret value is rendered.

## Evidence artifacts

At minimum:

- completed `proof/proof-manifest.json`;
- command transcripts under `proof/transcripts/`;
- changed-file inventory;
- architecture/reference artifacts;
- focused behavior artifacts;
- completed `SESSION-HANDOFF.md`;
- updated root `STATUS.md` and traceability rows.

## Progression gate

Pass only when every acceptance criterion, architecture assertion, focused build/test, and
negative proof is backed by an artifact. On pass mark this subbundle `DONE`, unlock only
`SB09`, and update the owning review.

On failure, keep downstream work locked. Do not call a missing proof a residual risk.

## Reopen triggers

- SB07 backend gate is not PASS.
- Current provider UI moved to another module/component.
- A required backend status/error is missing; reopen owning backend subbundle.

## Execution checklist

- [ ] Current branch/commit/worktree captured.
- [ ] Mandatory skills loaded.
- [ ] Bundle and subbundle readiness validated.
- [ ] Dependencies are `DONE`.
- [ ] Before architecture/reference evidence captured.
- [ ] Scope implemented without widening.
- [ ] Affected production projects built.
- [ ] Test discovery recorded and nonzero.
- [ ] Focused positive/negative tests passed.
- [ ] Security/redaction checks passed where applicable.
- [ ] After architecture/reference evidence captured.
- [ ] Proof manifest completed with artifact hashes.
- [ ] Session handoff completed.
- [ ] Status/traceability/review updated.
