# CanDoItAll.Plugin.Docker

## Purpose

Bundled Docker plugin for workflow executors that list containers, pull images, start containers, and read logs through governed host-tool recipes.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/plugins/CanDoItAll.Plugin.Docker/CanDoItAll.Plugin.Docker.csproj
```

## References

Project references:

- `../../CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `../../CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../../CanDoItAll.Modules.Plugins/CanDoItAll.Modules.Plugins.csproj`
- `../../CanDoItAll.Plugins.Abstractions/CanDoItAll.Plugins.Abstractions.csproj`
- `../../CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`

Framework references:

- None

Direct package references:

- `Microsoft.Extensions.DependencyInjection.Abstractions (10.0.7)`
- `Microsoft.Extensions.Logging.Abstractions (10.0.7)`

## Architecture Notes

Docker operations are host commands and must remain gated by plugin installation state, workflow executor grants, and host-command recipe grants. Do not bypass `PluginGrantEvaluator` or `IPluginHostToolService`.

Keep executor settings typed through `DockerWorkflowExecutorSettings` and keep command output capture policies explicit because Docker operations can be long-running and host-affecting.

## Related Docs

- Plugin module: `src/CanDoItAll.Modules.Plugins/README.md`
- Repository overview: `README.md` at the repo root
