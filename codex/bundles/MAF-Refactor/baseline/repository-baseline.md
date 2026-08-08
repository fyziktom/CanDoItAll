# Repository baseline

## Revision

- Branch: `development`
- Baseline commit: `51d9a2f071e9a5f295abac884c8c667328462cc4`
- Baseline commit message: `Merge branch 'development'`
- Baseline commit timestamp: `2026-08-05T20:00:47Z`

Claude Code must verify the current branch before editing. If HEAD has advanced:

1. keep this baseline as historical evidence,
2. inspect all changes touching listed hotspots,
3. refresh the responsibility and dependency inventory,
4. update the proof manifest with the new starting commit,
5. do not silently assume the architecture is unchanged.

## Primary projects

- `src/MAF/Common/CanDoItAll.AgentFramework.Models`
- `src/MAF/Common/CanDoItAll.AgentFramework.Core`
- `src/MAF/Common/CanDoItAll.AgentFramework.Maf`
- `src/MAF/Common/CanDoItAll.AgentFramework.Hosting`
- `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter`
- `src/Modules/CanDoItAll.Modules.AgentFramework`
- `src/Modules/CanDoItAll.Modules.Workbench`
- `src/Modules/CanDoItAll.Modules.Processes`
- `src/Modules/CanDoItAll.Modules.Security`

## Primary tests

- `tests/Unit/CanDoItAll.Tests.Unit`
- `tests/Components/CanDoItAll.Tests.Components`
- `tests/Integration/CanDoItAll.Tests.Integration`

## Baseline validation commands

Use the repository's configured SDK and restore policy. A typical sequence is:

```powershell
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --no-restore
dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-build
dotnet test tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --no-build
dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build
```

If the complete integration suite is environment-dependent, run the targeted filters from `plan/validation-matrix.md` and record every skipped prerequisite.
