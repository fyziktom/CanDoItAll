# Requirement Traceability

## Raw Note Closure Matrix

| Raw note | Requirement mapping | Impacted surface | Planned proof | Owning subbundle |
| --- | --- | --- | --- | --- |
| `N001` Simplify menu orientation from the keyboard. | `RQ-03`, `RQ-06` | Runtime keyboard routing, help docs | Playwright menu-flow proof plus help screenshots | `02-runtime-keyboard-navigation-and-menu-affordances`, `03-help-modal-information-architecture-and-shortcut-docs` |
| `N002` Single-letter shortcuts should select menu items. | `RQ-01`, `RQ-02`, `RQ-03` | Shared action contract, catalog assignment, runtime routing | Catalog tests plus browser keyboard flow | `01-shortcut-contract-and-catalog-foundation`, `02-runtime-keyboard-navigation-and-menu-affordances` |
| `N003` First key on an open menu should open the matching second-layer menu. | `RQ-03` | Context-menu state machine | Playwright nested submenu progression | `02-runtime-keyboard-navigation-and-menu-affordances` |
| `N004` Preserve requested block shortcuts. | `RQ-02` | Project-structure create catalog | Catalog and adapter assertions | `01-shortcut-contract-and-catalog-foundation` |
| `N005` Preserve requested asset shortcuts. | `RQ-02` | Project-structure create catalog | Catalog and adapter assertions | `01-shortcut-contract-and-catalog-foundation` |
| `N006` Preserve requested marker, meeting, people, infrastructure, note, and work shortcuts. | `RQ-02`, `RQ-03` | Catalog plus runtime routing | Catalog tests plus browser menu flow | `01-shortcut-contract-and-catalog-foundation`, `02-runtime-keyboard-navigation-and-menu-affordances` |
| `N007` Add shortcuts for other right-menu options too. | `RQ-02` | Node action menus and unlisted create siblings | Collision-free sibling-set tests | `01-shortcut-contract-and-catalog-foundation` |
| `N008` Add a better-structured help modal with browsable docs pages. | `RQ-06` | Help overlay markup and styling | bUnit assertions plus browser screenshots | `03-help-modal-information-architecture-and-shortcut-docs` |
| `N009` Underscore the letter used for the shortcut in menu items. | `RQ-04` | Menu label rendering and accessible naming | Browser screenshots plus DOM assertions | `02-runtime-keyboard-navigation-and-menu-affordances` |
| `N010` Split `03-interaction-and-state.js` if possible for maintainability. | `RQ-07` | Runtime module boundaries and asset boot order | Build plus route-load browser confirmation | `02-runtime-keyboard-navigation-and-menu-affordances` |

## Requirement To Bundle Mapping

| Requirement | Primary bundle location | Primary proof | Primary subbundle | Notes |
| --- | --- | --- | --- | --- |
| `RQ-01` | `architecture/01-target-solution.md`, `subbundles/01-shortcut-contract-and-catalog-foundation/README.md` | Component tests for action metadata and assignment helper | `01-shortcut-contract-and-catalog-foundation` | Shared contract must exist before runtime or docs can consume it. |
| `RQ-02` | `requirements/01-normalized-requirements.md`, `subbundles/01-shortcut-contract-and-catalog-foundation/README.md` | Catalog and adapter tests covering fixed mappings plus collision-free fallback | `01-shortcut-contract-and-catalog-foundation` | Includes extra menu families not explicitly listed in the request. |
| `RQ-03` | `architecture/01-target-solution.md`, `subbundles/02-runtime-keyboard-navigation-and-menu-affordances/README.md` | Playwright nested-keyboard route proof | `02-runtime-keyboard-navigation-and-menu-affordances` | Must hold across second-layer and third-layer menus. |
| `RQ-04` | `subbundles/02-runtime-keyboard-navigation-and-menu-affordances/README.md` | Browser screenshots and rendered markup assertions | `02-runtime-keyboard-navigation-and-menu-affordances` | Underline and actual runtime key must match. |
| `RQ-05` | `subbundles/02-runtime-keyboard-navigation-and-menu-affordances/README.md`, `subbundles/04-browser-proof-and-closure/README.md` | Focused regression tests plus browser smoke | `02-runtime-keyboard-navigation-and-menu-affordances`, `04-browser-proof-and-closure` | Protect editable inputs and existing global shortcuts. |
| `RQ-06` | `architecture/01-target-solution.md`, `subbundles/03-help-modal-information-architecture-and-shortcut-docs/README.md` | bUnit plus browser screenshots of help pages | `03-help-modal-information-architecture-and-shortcut-docs` | Help docs should stay aligned with real shortcut contract. |
| `RQ-07` | `analysis/01-current-state.md`, `subbundles/02-runtime-keyboard-navigation-and-menu-affordances/README.md` | Build plus browser route-load proof | `02-runtime-keyboard-navigation-and-menu-affordances` | Focused extraction only; avoid broad workbench rewrite. |
| `RQ-08` | `reviews/01-execution-report.md`, `subbundles/04-browser-proof-and-closure/README.md` | Updated execution report, screenshots, validator output | `04-browser-proof-and-closure` | Bundle cannot close with implicit or missing proof. |
