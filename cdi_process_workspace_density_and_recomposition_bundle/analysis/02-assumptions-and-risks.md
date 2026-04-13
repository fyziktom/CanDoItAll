# Assumptions And Risks

## Assumptions

- The definition canvas shown on `/processes` is the required surface. If the runtime canvas shares the toolbar contract, parity is welcome but not required for closure unless the user later extends scope.
- The existing `SummaryTile` component should gain a new opt-in visual mode instead of introducing a second metric component.
- `ViewportController` or the surrounding process-canvas host can be adjusted without rewriting the whole zoom stack.
- The managed SQLite profile is locally accessible and can be exercised through the app without environment approval barriers.

## Critical Path Risks

- A naive attempt to move all process-layout intelligence into CanvasLib would leak process semantics into a shared library and create a poor long-term boundary.
- A naive collision solver can remove overlap but still destroy the readable process narrative by scattering the main sequence.
- `subbundles/02` is a critical path phase because downstream persistence work depends on a stable shared contract.
- `subbundles/03` is a critical path phase because the final closure depends on persisted real-data proof, not just shared math.

## Validation Risks

- Hover-only menus can become inaccessible or fragile if they do not also preserve button and focus semantics.
- Width-usage regressions may only appear at specific zoom and viewport combinations, so one screenshot is not enough.
- Database proof can be weak if the verification step does not identify the specific recomposed definition and changed coordinate rows.

## Reopen Triggers

- Reopen `subbundles/01` if the final browser pass still shows dead width or summary-tile wrapping regressions.
- Reopen `subbundles/02` if the process integration ends up bypassing the shared contract or pushing process semantics into CanvasLib.
- Reopen `subbundles/03` if persisted coordinates do not survive reopen in the managed SQLite workspace.
