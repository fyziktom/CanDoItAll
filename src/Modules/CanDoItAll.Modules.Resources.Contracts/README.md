# CanDoItAll.Modules.Resources.Contracts

## Purpose

Stable Resource metadata, connector configuration values, project admission and typed Registry/Browse owner ports. Existing namespaces, enum values and serialized shapes are retained. EF entities, connector execution, file authorization and persistence stay in the Resources module.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework: `net10.0`

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Resources.Contracts/CanDoItAll.Modules.Resources.Contracts.csproj --configuration ResourcesUiProof /m:1
```

## Dependencies

The evaluated dependency inputs are in [CanDoItAll.Modules.Resources.Contracts.csproj](CanDoItAll.Modules.Resources.Contracts.csproj).
The [Resources boundary receipt](../../../docs/architecture/resources-ui-boundary.md)
records the evaluated source-mode closure, validation and compatibility decisions.

## Architecture Notes

FileBrowser sessions and read-only content leases are scoped rendering inputs. The owner supplies the release callback; each lease releases once. Source keys and project lifetimes describe origin, not permission.
