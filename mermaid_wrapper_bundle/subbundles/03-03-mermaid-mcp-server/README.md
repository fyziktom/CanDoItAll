# 03-mermaid-mcp-server

## Status

- `Completed`

## Objective

Add `CanDoItAll.Mcp.Mermaid`, a dedicated MCP server that exposes Mermaid v11.14.0 syntax guidance, advanced diagram guidance such as architecture-beta, examples, and graph-type-specific forbidden-symbol rules.

## Covered Inputs

- N005, N009, N010, N011
- Requirements R008, R009, R010

## Prerequisites

- Bundle prepared gate passed.
- Mermaid source/docs inspected from `C:\repositories\mermaid`.
- Subbundle 01 does not have to be complete unless implementation chooses to share constants; the MCP catalog should not depend on the Blazor wrapper.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\CanDoItAll.Mcp.Components.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Tools\ComponentsTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Catalog\ComponentCatalogModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Configuration\McpServerOptions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Core\Hosting\McpHostBuilderExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Core\Contracts\McpToolEnvelope.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Components.Tests\ComponentCatalogServiceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Components.Tests\ComponentsToolsTests.cs`
- `C:\repositories\mermaid\docs\syntax\architecture.md`
- `C:\repositories\mermaid\packages\parser\src\language\architecture\architecture.langium`
- `C:\repositories\mermaid\packages\parser\src\language\architecture\arch.langium`
- `C:\repositories\mermaid\packages\parser\src\language\common\common.langium`

## Deliverables

- New MCP server project `src/CanDoItAll.Mcp.Mermaid`.
- Root settings file `CanDoItAll.Mcp.Mermaid.settings.json`.
- Catalog models/service with Mermaid version/source references.
- Tools for search, diagram detail, syntax rules, forbidden symbols, and examples.
- README documenting purpose and validation command.
- New test project `tests/CanDoItAll.Mcp.Mermaid.Tests`.
- Solution membership updates.

## Dependency Impact

- This subbundle is required to close the MCP portion of the raw request.
- Future agents depend on forbidden-symbol guidance to avoid invalid generated Mermaid.

## Validation Depth

- `Process-critical closure`
- Unit tests for catalog completeness and tool structured responses.

## Implementation Steps

1. Create MCP project following `CanDoItAll.Mcp.Components` host pattern.
2. Create configuration/options validator with workspace root and optional Mermaid docs root.
3. Build static catalog entries from Mermaid v11.14.0 docs/source.
4. Include graph-type-specific forbidden-symbol guidance:
   - architecture-beta IDs: alphanumeric/underscore start; IDs may contain dashes/underscores but cannot end with dash; icon tokens only word/dash/colon inside `()`; unquoted titles only word characters and spaces; use quotes for punctuation; edge ports only `L`, `R`, `T`, `B`; group edge modifier is literal `{group}`.
   - flowchart: avoid lowercase `end` as a node id; quote or escape labels with special characters; avoid leading `o` or `x` immediately after edge dashes unless an edge marker is intended.
   - sequence: actor/participant aliases should be simple identifiers; messages containing punctuation should stay after the colon; activation/deactivation symbols must follow sequence grammar.
   - class/state/ER/block/xychart and other common types: include known delimiter and quoting rules from Mermaid docs.
5. Add tools returning `McpToolEnvelope<T>` and deterministic errors for unknown diagram types.
6. Add tests that verify architecture-beta rules, forbidden symbols, search, examples, unknown type errors, and tool envelopes.
7. Add project/test project to `CanDoItAll.slnx`.

## Scope Exceptions

- The MCP server does not validate arbitrary Mermaid source in this phase.
- The MCP server does not render diagrams.
- The first catalog does not need exhaustive full grammar coverage for every Mermaid diagram, but it must cover main rules and high-risk forbidden symbols for listed diagram types.

## Do Not Do

- Do not depend on browser or the Mermaid wrapper package.
- Do not copy long Mermaid docs verbatim.
- Do not expose write/mutation tools.

## Acceptance Checklist

- `CanDoItAll.Mcp.Mermaid` builds.
- Tools return structured MCP envelopes.
- `architecture-beta` guidance includes group/service/junction/edge syntax, `randomize`, icons, and forbidden symbols.
- Forbidden symbol guidance exists per covered graph type.
- Tests pass.

## Proof Required

- `dotnet build src/CanDoItAll.Mcp.Mermaid/CanDoItAll.Mcp.Mermaid.csproj`
- `dotnet test tests/CanDoItAll.Mcp.Mermaid.Tests/CanDoItAll.Mcp.Mermaid.Tests.csproj`

## Browser Validation Logging

- N/A. This subbundle does not affect browser-visible UI.

## Progression Gate

- Final closure may continue only after MCP build/tests pass and execution report records the tool coverage for syntax and forbidden symbols.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Add a dedicated read-only Mermaid MCP server and tests using existing CanDoItAll MCP patterns. Include architecture-beta and graph-type-specific forbidden-symbol guidance. Do not render diagrams or depend on browser UI.
```
