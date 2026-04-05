# Assumptions and risks

## Assumptions

- The user still wants node identity to remain stable even when the node evolves from note/brainstorm capture to a richer operational form.
- Semantic X/Y placement and markers are part of the canonical project meaning, not merely UI decorations.
- The upcoming plugin wave will eventually include connectors with outbound or externally visible side effects.
- The desired end-state is not “many disconnected tables with no node center”; the desired end-state is “stable universal carrier node with explicit typed facets and bindings”.

## Risks in this review

- Runtime behavior could differ because the environment did not provide `dotnet`.
- Some intended refactor work may already exist on another branch not included in this zip.
- A few static checks in the hard-gate script are deliberately strict so repeated unresolved blockers cannot be closed by interpretation alone.
