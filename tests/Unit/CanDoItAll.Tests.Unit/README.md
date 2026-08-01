# CanDoItAll.Tests.Unit

## Purpose

Broad unit and architecture-regression suite for the repository. MCP-specific tests live
in the sibling `CanDoItAll.Mcp` repository.

## Prerequisites

The retained RAG and Semantic Completion compatibility dependencies restore from
NuGet.org; their sibling source repositories are not required.

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
- Current architecture: `docs/architecture/overview.md`
