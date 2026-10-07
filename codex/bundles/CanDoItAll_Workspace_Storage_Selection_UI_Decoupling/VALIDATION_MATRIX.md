# Validation matrix

These are semantic proof requirements, not a test-count quota. Map each row to actual current tests, expected/discovered/executed cases and artifacts. Existing tests may satisfy several rows, but do not double-count their execution. Failures and unavailable gates remain explicit. [EV05–EV08]

## S0: bounded catalog fix

| ID | Scenario / required assertion | Layer |
|---|---|---|
| V-S0-01 | Actual highlighted CatalogSurface row reactivated after name/raw-number/step edits: same acquired draft and EditContext, raw text and validation retained, no additional exact read | State + real renderer; failing-first |
| V-S0-02 | Same-ID unacquired/failed/missing target still retries, deleted target not treated as editable, new/different/A→B→A transitions retain correct ownership | State + renderer |
| V-S0-03 | Held Save/Test/read-back plus same-row activation: no second effect, receipts unchanged, new input and committed identity remain on correct draft | Controlled state + renderer |
| V-S0-04 | Real production Settings Storage interaction; API current-denial/idempotent-disposal and Core Files identity regressions still pass | Production browser + focused existing tests |

## Selection state and actual interaction

| ID | Scenario / required assertion | Layer |
|---|---|---|
| V-SE-01 | Current catalog requested per new chooser; empty field has no eager read; saved-ID labels resolve without opening | Controlled owner + real field/dialog |
| V-SE-02 | No-op parameter echo preserves staging, parent/field identity and read counts; local search/toggles cause no backend write/extra read | State + renderer |
| V-SE-03 | Missing and disabled saved IDs remain visible/removable; newly disabled cannot be added; read-only allowed; Guid.Empty/duplicates handled canonically | Handler + renderer |
| V-SE-04 | Apply current staged list once; Cancel/Escape/parent close/disposal emit none; no durable parent save until explicit Save | Handler + actual dialogs |
| V-SE-05 | Parent target A→B→A with equal selected GUIDs, new owner and changed callback: old dialog never publishes into successor | Controlled parent fixture |
| V-SE-06 | Parent selection revision changes while picker open; late Apply cannot overwrite new IDs; ordinary echoed selection is not a false invalidation | Controlled parent + actual field |
| V-SE-07 | AllowAll/Disabled flips while open, including away-and-back; late confirmation cannot edit protected state; disabling and re-enabling preserves explicit list | Handler + renderer |
| V-SE-08 | Source/profile replacement with same catalog IDs does not reuse old names or labels; only current snapshot can establish missing state | Controlled profile/source fixtures |
| V-SE-09 | Old success/failure/CatalogsLoaded/finally after newer read cannot replace cache, failure or busy state; include cancellation-ignoring completion | State + renderer |
| V-SE-10 | Initial failure, successful empty, retained stale snapshot and actual absent ID remain distinct; retry preserves selected/staged IDs | Real field/dialog |
| V-SE-11 | Direct Apply while loading/error/retired, and direct toggle of fabricated unknown ID, are rejected without relying on disabled markup | Handler regression tests |
| V-SE-12 | Two fields/parents and nested dialogs are independent; closing one closes only its owned overlays and retains the other's selection/read | Actual BaseLib DialogHost + browser |
| V-SE-13 | Repeated disposal, close/reopen, queued result and noncooperative read cannot throw or leak an owned dialog/subscription | Lifecycle tests |
| V-SE-14 | Safe error/projection contains no raw credential/configuration/exception sentinel; no secret resolver called to render metadata | Projection + renderer |

## Production and architecture

| ID | Scenario / required assertion | Layer |
|---|---|---|
| V-PR-01 | Actual Agent details field uses new renderer and current parent-session guard; no old backend renderer remains hidden behind a facade | Real production host |
| V-PR-02 | Apply stages exact IDs only, parent Cancel does not persist; parent Save stores same IDs and existing flags; reopen/read-back verifies data | Real Agent persistence + browser |
| V-PR-03 | Parent instructions/capabilities/project/secret/file settings and EditContext survive picker operation; no implicit AllowAll/read/write change | Production component/persistence |
| V-PR-04 | Saved allowlist restricts real Storage tool catalog listing/browse/disclosure; disallowed target denied/filtered, read-only constraints preserved | Existing runtime owner integration |
| V-PR-05 | Profile/caller retirement and parent reopen use current source; no store/profile switching is performed by the picker | Host + isolated profile test |
| V-BD-01 | Actual evaluated restore graph, runtime/public-type closure and all rendered descendants are free of forbidden implementations | Boundary tests + graph artifacts |
| V-BD-02 | Negative transitive, unresolved and cycle fixtures fail correctly; don't mistake a preserved namespace for an assembly edge | Boundary tests |
| V-BD-03 | Core/API/catalog-admin sandbox graphs/watch sets acquire no new selection edge; Foundation/MAF/AppComponents acquire no reverse feature edge | Evaluated comparison + source/assembly guards |
| V-BD-04 | Real AppComponents picker/table reused; no duplicate generic code, fake child or forced whole-family move; exact assets publish | Build + runtime + publish |
| V-BD-05 | Current compiled consumers, DI, Razor imports, test solutions and actual CI membership updated; no product test-project leakage | Builds + inventory |

## Sandbox, application and closure

| ID | Scenario / required assertion | Layer |
|---|---|---|
| V-UI-01 | Representative/empty/large/missing/disabled/read-only/AllowAll/loading/failure/reset/held scenarios use same production feature code | Sandbox + component tests |
| V-UI-02 | Real Web nested Agent dialog with keyboard/search/Apply/Cancel, parent Save/reopen; no unexpected circuit/console/asset errors | Playwright |
| V-UI-03 | Source and separately published Production sandbox run without DB/vault/runtime; real font/styles/overlay behavior verified | Playwright + publish |
| V-UI-04 | Large-desktop layout, bounded results viewport, parent/child stacking, scroll ownership and focus/caret validated visually and functionally | Browser inspection |
| V-UI-05 | Original/final graph/watch/visible Razor/C# loop measured with actual generic closure; no inherited fixed graph count | DEV_LOOP procedure |
| V-CL-01 | Application non-regression matrix executed with actual owners where specified | APPLICATION_REGRESSION_MATRIX |
| V-CL-02 | Current targeted discovery, builds and results recorded; full-Stable trigger assessed under current rules | Test ledger |
| V-CL-03 | Mandatory proposed-tree portability, reviewed baseline, final no-write enforcement, docs/evidence and secret gates pass or explicitly remain open | Repository gates |
| V-CL-04 | Exact owned process/container/root cleanup, unchanged sibling sources, signed local delivery and partial Workspace status | Final evidence |

## Existing tests to preserve and locate

Read current declarations before selecting filters. Known anchors: `StorageCatalogSelectionComponentsTests`, `CatalogStateTests`, `CatalogRendererTests`, `ApiReadLifetimeTests`, `WorkspaceFileIdentityTests`, `ResourceEditorReadinessTests`, generic `ResourceCardPickerTests` / `SelectedReferenceTableTests`, actual Agent details/session/workspace access tests, `StorageCatalogContractPersistenceTests`, Storage tool/attachment/disclosure tests and the existing Settings browser journeys. Only the named source files listed in SOURCES have been directly reviewed; executor inventory must confirm the remainder and their owning projects.

A migrated event test must drive the actual event (`input`, `change`, dialog result) rather than weaken the assertion to fit changed implementation. Await dispatched events and controlled read completion. Do not silently drop old eight-case picker semantics when relocating their tests. [WS06, EV08]
