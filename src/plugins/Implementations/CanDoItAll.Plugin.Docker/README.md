# CanDoItAll.Plugin.Docker

## Purpose

Bundled Docker plugin for workflow executors that list containers, pull images, start containers, and read logs through governed host-tool recipes.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/plugins/Implementations/CanDoItAll.Plugin.Docker/CanDoItAll.Plugin.Docker.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Plugin.Docker.csproj](CanDoItAll.Plugin.Docker.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Docker operations are host commands and must remain gated by plugin installation state, workflow executor grants, and host-command recipe grants. Do not bypass `PluginGrantEvaluator` or `IPluginHostToolService`.

Keep executor settings typed through `DockerWorkflowExecutorSettings` and keep command output capture policies explicit because Docker operations can be long-running and host-affecting.

## Related Docs

- Plugin module: `src/Modules/CanDoItAll.Modules.Plugins/README.md`
- Repository overview: `README.md` at the repo root
