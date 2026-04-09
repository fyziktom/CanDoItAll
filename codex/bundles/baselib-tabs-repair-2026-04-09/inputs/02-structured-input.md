# Structured Input

## Bundle Goal

- Repair the shared BaseLib tabs component and its proof surface so the component is visually coherent, customization-friendly, Tailwind-owned, and validated against happy-path plus edge-case examples.

## Hard Constraints

- Keep the shared contract Tailwind-owned. Do not hand-edit `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\wwwroot\css\output.css`.
- Do not introduce new shared non-canvas `zy-*` selectors.
- Final implementation must use the existing CanDoItAll `cad-*` and `cda-*` token contract, not Radzen styles.
- Preserve the current component’s accessibility and keyboard behavior unless a deliberate fix improves it.
- Expose customization through parameters and standard class extension points instead of page-local CSS hacks.
- Provide a dedicated sandbox tabs page or dedicated tabs route, not only a mixed navigation page section.
- Prove the result with real browser automation plus screenshots on a large-screen pass and narrower-width follow-up passes.

## Input Inventory

- `N001` Repair the current tabs visual styling because it “kind of works” but does not look correct.
- `N002` Unify away from the current split between `zy` and `cad` style groups.
- `N003` Support appearance customization through parameters, enums, and root `Class` extension.
- `N004` Use Radzen only as behavior and visual reference; final styling must be Tailwind-only.
- `N005` Add a dedicated tabs page in the components sandbox.
- `N006` Add examples for non-optimal paths such as long title, missing title, and wrapping on smaller widths.
- `N007` Treat the examples as a discovery surface: if new issues appear, reopen the relevant earlier subbundle and repair it before closure.
- `N008` Prefer a light border around tab buttons, but make it optional.
- `N009` Analyze first and execute through a bundle with subbundles.
- `N010` Validate the result with browser automation and screenshots, and confirm the tabs “are looking”.

## Current-State Highlights

- The live BaseLib `Tabs` component currently emits both `cad-tabs*` and `zy-tabs*` classes, but the real styling sits in `Tabs.razor.css` under the `zy-tabs*` selectors.
- The scoped CSS currently hard-codes a purple-leaning look that diverges from the shared `cad` token family in `Tailwind/foundation/theme.css`.
- `Tailwind/navigation/tabs.css` already exists but currently styles `cda-tab-strip` and inline-tab helpers rather than the shared `Tabs` component.
- `Tabs` does not currently inherit `StyledComponentBase`, so it lacks the standard `Class` and `Style` parameters used elsewhere for root-level extension.
- The sandbox only exposes one mixed navigation example on `/groups/navigation`; there is no dedicated tabs lab route yet.
- There are no dedicated BaseLib `Tabs` component tests yet.

## Validation Expectations

- Run the bundle readiness validator before implementation.
- Use the managed CanDoItAll watch backend for local UI iteration.
- Use terminal-driven Playwright automation in a headed browser, capture screenshots to `output/playwright/baselib-tabs-repair-2026-04-09/`, and review them explicitly.
- Validate desktop first, then narrower widths on the same route.
- Update the execution report immediately while proof is fresh.
