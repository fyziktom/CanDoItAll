# Target Solution

## Shared Tabs Contract

- Refactor `Tabs` toward the repo’s normal shared-component contract:
- root extensibility through the standard `Class` pattern or an equivalent existing BaseLib mechanism
- appearance choices through enum-backed parameters instead of selector branching hidden in page CSS
- no shared dependency on `zy-*` selectors
- Keep the current behavior model for selection, render mode, tab position, visibility, disabled items, icons, badges, and accessible tab roles.

## Styling Strategy

- Move the shared tabs appearance into Tailwind-owned sources, primarily `C:\repositories\CanDoItAll\Tailwind\navigation\tabs.css`.
- Use `C:\repositories\CanDoItAll\Tailwind\foundation\theme.css` for any additional token-level needs rather than hard-coded colors.
- The final selector family may use the existing `cad-*` and `cda-*` convention, but it must be a single stabilized CanDoItAll contract with no new shared `zy-*` dependency.
- Rebuild the generated stylesheet through the Tailwind pipeline after source changes.

## Sandbox Strategy

- Add a dedicated tabs route in the sandbox, likely under the navigation group, so the component can be judged on its own instead of as a small section of a broader page.
- The tabs route should include a compact set of intentional examples that cover:
- default healthy usage
- optional border appearance
- missing title fallback
- long title handling
- narrow-width or small-column stress
- at least one variant or orientation comparison when useful
- The page should use shared BaseLib layout components and avoid page-local CSS that hides component flaws.

## Validation Strategy

- Treat subbundle 01 as a critical UI foundation and require both test proof and browser proof before sandbox work continues.
- Reuse one managed watch session for the sandbox project during UI work.
- Use headed Playwright CLI proof for:
- desktop baseline on the dedicated tabs route
- targeted interactions such as clicking tabs and observing active-state changes
- narrower-width passes after the desktop state is accepted
- Capture screenshots into `output/playwright/baselib-tabs-repair-2026-04-09/` and record screenshot review answers in the execution report.
