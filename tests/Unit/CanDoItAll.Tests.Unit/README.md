# CanDoItAll.Tests.Unit

## Purpose

Broad unit and architecture-regression suite for the repository. MCP-specific tests live
in the sibling `CanDoItAll.Mcp` repository.

## Prerequisites

The unit project retains compatibility coverage that references two sibling source
repositories. Clone them beside this repository before restoring or running the project:

```text
<parent>\
  CanDoItAll\
  CanDoItAll.AgentFramework.Rag\
  CanDoItAll.AgentFramework.SemanticCompletion\
```

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Tests.Unit.csproj](CanDoItAll.Tests.Unit.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Keep tests focused on observable behavior and use shared fixtures from CanDoItAll.Tests.Support where cross-project setup is needed.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
