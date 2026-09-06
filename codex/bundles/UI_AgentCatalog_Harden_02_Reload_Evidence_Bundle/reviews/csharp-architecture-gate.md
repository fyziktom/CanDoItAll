# Architecture closure

The only sandbox behavior changes are the immutable CatalogSandboxContext query contract and the existing Catalog page consuming it. One record owns scenario/layout/selection; computed aliases pass its selection to the existing controlled panel. Explicit stable URL tokens avoid enum ordinals. The page uses platform NavigationManager with replace-history semantics; invalid IDs are removed against the embedded fixture. No new service, project, production route, Manager integration, asset-mode branch, or sibling change exists.

A separate context type is justified by independently tested parsing and fixture validation. Keeping all parsing in Razor would couple the contract to rendering; a general navigation framework would exceed this development-only scope. No compatibility wrapper or extra partial was introduced. The real panel/card/tree/tooltips and established asset projects remain unchanged. Direct UI, Parity and Fast builds and real browser mode checks prove continued composition; the same 12-project lightweight graph is preserved by zero project/reference changes.

Components MCP was attempted but returned Transport closed. The actual live sibling Button, Stack and Cluster contracts were read; their usage and source were unchanged. This is a documented tool availability limitation, not a fallback implementation.

Public tests: 13 context cases plus five mode cases; 30 owning catalog rendering/lifetime cases. Negative browser proof reproduced the original query failure. Both real mode hosts then passed eight reload-context checks each and existing asset/tooltip/card/empty/loading/avatar acceptance. Screenshots were inspected for restored selected card/team, Fast card states and tooltip layout.

One intentional portability baseline addition permits OrdinalIgnoreCase for the external sandbox layout query token, not for filesystem identity. Final enforcement passed with 14,298 reviewed findings. No other baseline policy or count was relaxed.
