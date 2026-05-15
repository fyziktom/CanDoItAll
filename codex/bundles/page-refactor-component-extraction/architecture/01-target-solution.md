# Target Solution

## Boundaries

- Pages own routing, service orchestration, high-level state, navigation, and cross-component event wiring.
- Helper classes own pure or mostly pure transformations, display labels, filtering predicates, action keys, editor model creation, and typed classification rules.
- Extracted Razor components own a coherent visual section, receive state through typed parameters, and emit changes through explicit `EventCallback` members.
- Existing module services, domain models, and infrastructure services remain the source of truth for persistence and business rules.

## Helper Extraction Pattern

- Create module-local internal static helper classes near the consuming page when the helper is page-specific.
- Place helpers in a domain subfolder only when multiple pages or components already consume the same concept.
- Keep method names strongly typed and intent-revealing; avoid helper methods that accept string commands when an enum or existing value object exists.
- Add targeted unit tests for helpers when they encode branching behavior not already covered through component tests.

## Component Extraction Pattern

- Extract one visible region at a time.
- Pass immutable snapshots or typed models downward where possible.
- Pass callbacks upward as `EventCallback` or typed delegate parameters only when the existing component pattern already uses delegates.
- Preserve existing `data-testid` values and CSS class hooks unless a subbundle explicitly records a coordinated update.
- Prefer BaseLib components such as `PageScaffold`, `Grid`, `Row`, `Column`, `Stack`, `SectionHead`, `SectionCard`, `StatsGrid`, and CanvasLib workbench components before page-local structural markup.

## Proof Strategy

- Use component and unit tests for helper behavior and event wiring.
- Use Playwright for route-level browser proof after UI component extraction.
- Capture screenshots for changed ProjectStructure, PromptFactory, Plugins, CRM/HR, Settings, and Workflow surfaces.
- Keep proof paths and gate decisions synchronized in `reviews/01-execution-report.md`.
