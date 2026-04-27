# Current State

## MCP Project Graph

- CodeAnalytics snapshot `snap-20260426215347-aec8ae51` found 8 scoped MCP projects and 122 scoped documents.
- `CanDoItAll.Mcp.Core` is referenced by all server projects except no project references it upstream.
- `CanDoItAll.Mcp.DotNetWatch` additionally references `CanDoItAll.Mcp.LocalRuntime`.
- Server projects use `ModelContextProtocol` directly and register their own tool classes in `Program.cs`.

## Repeated Host Setup

- `CodeAnalytics`, `Components`, `Processes`, `ProjectStructure`, and `SshOps` repeat the same startup pattern: resolve settings path, add JSON file, add `CanDoItAllMcp_` environment variables, clear logging providers, send console logging to stderr, bind options, register validator, and add stdio MCP server tools.
- `DotNetWatch` repeats most of the same host setup in a larger dual host (`stdio proxy` plus backend HTTP host), with a separate backend logging mode.
- The repeated setup makes future host changes easy to miss in one server and hard to test in isolation.

## Long Files And Responsibility Hotspots

Line-count inventory, excluding `bin` and `obj`, identified these prominent hotspots:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Catalog\ComponentCatalogService.cs` around 1680 source lines, mixing static catalog metadata, search behavior, reflection, CSS source lookup, and consumer usage scanning.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Runtime\AppRuntimeModels.cs` around 1623 source lines, combining template records, app session state, log buffers, status data, and runtime enums.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Runtime\SessionCoordinator.cs` around 1207 source lines, coordinating app lifecycle, operations, logs, waits, builds, tests, and failures.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.DotNetWatch\Program.cs` around 395 source lines, combining launch mode selection, stdio host, backend host, DI setup, route mapping, launch context parsing, and cleanup.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.SshOps\Coordination\TargetCoordinator.Compose.cs` around 707 source lines and related partials, already partly split but still carrying complex target coordination logic.

## Existing Tests

- MCP-specific test projects exist for Components, DotNetWatch, Processes, and ProjectStructure.
- No dedicated `CanDoItAll.Mcp.Core.Tests` project exists, so shared helper proof should initially live in an existing MCP test project that already references the affected server stack.
- Component catalog tests already cover catalog behavior and can prove that metadata extraction survives file splitting.
- DotNetWatch tests already cover infrastructure and runtime behaviors and can prove shared host helpers and program route splitting compile.
