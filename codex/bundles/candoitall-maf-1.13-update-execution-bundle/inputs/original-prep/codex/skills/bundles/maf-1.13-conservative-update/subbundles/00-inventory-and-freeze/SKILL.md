# Subbundle 00: Inventory and Freeze

## Goal

Capture the current baseline before changing packages.

## Steps

1. Confirm branch and clean working tree.
2. Record package references in MAF adapter projects.
3. Search for all MAF-related package references.
4. Try baseline restore/build.
5. Record pre-existing failures separately.
6. Create an evidence note skeleton.

## Commands

```powershell
git status --short
dotnet --info
rg "Microsoft\.Agents\.AI|Microsoft\.Extensions\.AI" src tests tools -g "*.csproj"
dotnet list src\MAF\Common\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj package
dotnet list src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.MafAdapter\CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj package
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore
```

## Exit criteria

- Baseline package list is recorded.
- Any existing failure is documented.
- No source package update has happened yet.
