# Subbundle 01: Package Version Update

## Goal

Update only the required MAF package references.

## Allowed files

- `src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj`
- generated lock files, if present

## Required changes

- `Microsoft.Agents.AI` -> `1.13.0`
- `Microsoft.Agents.AI.OpenAI` -> `1.13.0`
- `Microsoft.Agents.AI.Workflows` -> `1.13.0`
- `Microsoft.Extensions.AI.Abstractions` -> `10.6.0`
- `Microsoft.Extensions.DependencyInjection.Abstractions` -> `10.0.9`

## Preview package rule

Run:

```powershell
dotnet list src\MAF\Common\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj package --outdated --include-prerelease
```

Update `Microsoft.Agents.AI.A2A` or `Microsoft.Agents.AI.Mem0` only if the CLI proves a compatible newer version exists. Otherwise keep them unchanged.

## Validation

```powershell
dotnet restore CanDoItAll.slnx
```

## Exit criteria

- Package restore succeeds or has a clear package-only failure.
- No application code changed in this subbundle.
