# CanDoItAll.AgentFramework.Maf

## Purpose

Microsoft Agent Framework adapter that connects CanDoItAll execution runs to provider runtimes, tools, skills, and MCP servers.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj
```

## References

Project references:

- `../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `../CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj`
- `../CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj`
- `../CanDoItAll.Modules.Workspace/CanDoItAll.Modules.Workspace.csproj`

Framework references:

- None

Direct package references:

- `Azure.AI.OpenAI (2.9.0-beta.1)`
- `Microsoft.Agents.AI (1.0.0)`
- `Microsoft.Agents.AI.Mem0 (1.0.0-preview.251028.1)`
- `Microsoft.Agents.AI.OpenAI (1.0.0)`
- `ModelContextProtocol (1.1.0)`
- `OllamaSharp (5.4.25)`

## Architecture Notes

Keep AgentFramework model contracts, persistence, provider-neutral orchestration, and provider/runtime adapters separated. Process automation should consume this layer through the AgentFramework module bridge instead of reaching into provider-specific code directly.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
