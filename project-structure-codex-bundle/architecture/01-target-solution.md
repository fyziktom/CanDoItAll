# Target Solution

## Ownership Split
- JavaScript owns scene rendering, retained element maps, hit testing, drag and pan loops, viewport culling, floating-window live geometry, and diagnostic counters.
- C# owns typed models, service logic, commands, transactions, graph adapters, and persisted committed state.
- HTML and Blazor stay responsible for toolbox, selection panels, dialogs, uploads, previews, summary surfaces, transcript confirmation, and mermaid viewing.

## Architectural Guardrails
- No TypeScript.
- No big-bang rewrite.
- Preserve the public workbench API unless the owning subbundle explicitly migrates it with tests.
- Prefer the smallest coherent change set per subbundle.

## Expected End State
- Hot-path interaction remains local to the browser until commit.
- Structural graph changes are reflected through narrower patches rather than repeated full rebuilds.
- The renderer path is measurable and regression-tested.
- Shared canvas ownership is clearer and easier to maintain.
