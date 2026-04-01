# Structured Input

## Core Objective

- Upgrade the shared project-structure canvas so it supports semantic per-category node visuals, richer note editing and conversion, typed block mutation, subtree-aware clipboard workflows, subtree-to-subproject transfer, and full browser-proof closure for the entire feedback set.

## Hard Constraints

- Preserve every raw note as a distinct closure target.
- Use the existing Tailwind theme system and existing component libraries instead of ad hoc CSS islands.
- Keep the color system modular and node-property-driven rather than scattering palette logic across adapters, components, and runtime JavaScript.
- Use real Playwright MCP validation plus screenshots for all browser-visible behavior, especially canvas interactions.
- Prefer the smallest maintainable change that fits the current architecture.

## Source Artifacts

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\04-context-menu-and-composer.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07-runtime-entry.js`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.cs`

## Input Coverage Signals

- `N001` cannot be reduced to a CSS polish request because the user explicitly requires node-property-driven color architecture.
- `N002` requires keyboard behavior changes in the actual inline note editor, not only dialog editing.
- `N003` distinguishes copying a single id from copying the descendant id structure.
- `N004` explicitly includes descendants in cut and paste, so shallow-node clipboard handling is insufficient.
- `N005` is a separate hierarchy-transfer flow and cannot be merged into generic cut and paste.
- `N006` is broader than new presets because it requires changing an existing block type.
- `N007` and `N009` both add catalog surface area and must appear in the same discoverable UI flows as existing common blocks.
- `N008` is not just type mutation because it starts from note content and changes the content mapping rules.

## Dependency And Sequencing Signals

- A unified visual preset architecture is foundational because later block additions and mutations must inherit it instead of re-implementing color logic.
- Catalog expansion and type mutation should land before note-to-block conversion so note conversion can reuse the same block preset and mutation infrastructure.
- Clipboard cut and paste needs explicit subtree serialization before subtree-to-subproject transfer can safely reuse similar descendant selection semantics.
- Final browser-proof closure must follow all implementation subbundles because the user explicitly rejected skipped UI testing.

## Validation Expectations

- Every subbundle that changes browser-visible behavior must have component or integration coverage where applicable plus Playwright MCP proof.
- Color-related proof must include both logic-level assertions and rendered browser evidence so palette regressions are not hidden by adapter-only tests.
- Keyboard and clipboard flows must be proven through actual runtime events, not only direct method invocation.
- Closure is blocked until screenshots are captured and reviewed for the relevant states.

## UI Validation Strategy

- Run a large-screen pass at `1600x1000` on `/projects/{projectId}/structure` for all subbundles that touch the canvas, toolbox, quick actions, or selection panel.
- Run a narrower follow-up pass at `1280x800` for subbundles that change window chrome, note editing layout, action density, or toolbox discoverability.
- Review screenshots for color distinctness, readable contrast, multiline note rendering, visible action affordances, and subtree operation clarity.

## Browser Validation Analytics

- Log, per subbundle, the route, viewport, Playwright actions, assertions, screenshot file names, and pass or reopen result in `reviews/01-execution-report.md`.
- Use screenshot names that identify the feature under proof, such as `01-visual-presets-large.png` or `04-cut-paste-subtree.png`.
- Treat flaky or timing-sensitive proof as a reopen condition until the interaction is stable enough to pass repeatedly.

## Working Assumptions

- The canonical surface under test remains `/projects/{projectId}/structure`.
- `Shift+Enter` adds a newline in inline note editing while plain `Enter` continues the existing commit behavior unless a richer editor contract proves necessary during implementation.
- The subtree id export should preserve hierarchy order in deterministic root-first text rather than an unordered set of ids.
- Cut and paste of a subtree should create new node ids at the destination while preserving internal parent-child relationships and relative layout.
- The subtree-to-subproject flow moves descendants under the selected node into a chosen subproject target while leaving the selected anchor node in the current project.

## Primary Risks

- Visual preset logic is currently split across multiple layers, so a partial fix would leave the code harder to maintain than before.
- Browser clipboard and keyboard flows span C#, JavaScript runtime, and persisted project structure mutations, which raises desynchronization risk.
- Subtree-to-subproject transfer may expose missing backend primitives or hidden assumptions in recomposition and hierarchy-link code.
- Screenshot-only review is insufficient if color assertions are not backed by deterministic test selectors or computed-style checks.
