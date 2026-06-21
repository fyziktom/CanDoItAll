# SB14 Semantic Invariants

## Invariants Preserved

- Definition catalog UI reads a projection DTO, not runtime tables, EF entities, or old Process observation services.
- Definition counts, visible items, selected definition, search text, and scope groups come from `ProcessDefinitionCatalogProjection`.
- Search and scope filtering are application projection behavior. The component only forwards typed query state and renders the returned projection.
- Feed Defaults is a typed application command returning a receipt and refresh token; it does not silently mutate UI state without explicit command proof.
- Template defaults are loaded from canonical JSON (`manifest.json` and `definition.json`) through `ProcessTemplatePackLoader`; Markdown and Mermaid are not canonical inputs.
- UI module dependency boundary remains `CanDoItAll.Processes.Application` plus `CanDoItAll.Processes.Projections`; the UI module does not reference `Processes.Templates`, JSON APIs, or file-system APIs.
- Project-scope catalog support is explicit: project-specific definitions currently return count zero and an empty state instead of falling back to global data without telling the user.

## Negative Proof

- `scans/ui-forbidden-runtime-persistence-scan.txt` has 0 matches for runtime, persistence, EF, DbContext, event, observation, and command implementation symbols in the owned UI/test surface.
- `scans/ui-no-template-or-file-dependency-scan.txt` has 0 matches for direct template-loader, JSON, file, or directory dependencies from `src/CanDoItAll.Modules.Processes`.
- `scans/anti-stub-scan.txt` has 0 matches for TODO, HACK, `NotImplementedException`, or stub markers in the owned SB14 source/test surface.
- `ProcessTemplatePackLoader` rejects manifest/definition key mismatch; proof is in `Loader_rejects_manifest_definition_key_mismatch`.

## Positive Proof

- `test-unit-definition-catalog-sb14.txt` passed 8/8 and covers catalog filtering, selected key resolution, Feed Defaults receipt/token creation, template-loader mismatch rejection, and existing process boundary tests.
- `test-components-process-shell-sb14.txt` passed 9/9 and covers search query forwarding, scope filter forwarding, Feed Defaults command boundary use, global/project shell state, refresh, agent context, and navigation contribution.
- `test-playwright-process-shell-sb14.txt` passed 1/1 and proves the real host renders `/processes`, searches/selects a definition, executes Feed Defaults, and still renders the project-scoped process route.
- Browser MCP proof shows desktop selected-definition receipt and narrow project-scope empty state with no visible Blazor error UI.

## Shallow-Pass Trap Rejected

A shallow implementation could hardcode 24 definitions in the component or search a static in-memory list. SB14 avoids that by introducing projection query DTOs, a catalog projection service, canonical template-pack loading, typed command receipt state, focused unit tests, and UI dependency scans.
