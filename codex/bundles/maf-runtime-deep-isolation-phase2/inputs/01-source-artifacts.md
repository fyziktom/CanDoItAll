# Source Artifacts

## Repository

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime`
- `repo://tests/Unit/CanDoItAll.Tests.Unit`
- `repo://tests/Integration/CanDoItAll.Tests.Integration`

## CodeAnalytics Snapshot

- Snapshot id: `snap-20260706154749-275f822a`
- Scope: `CanDoItAll.AgentFramework.Maf`
- Project path: `src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`
- Snapshot health: `ok`, 1 source project, 44 documents, no blocking diagnostics.

## Commands Used For Bundle Preparation

```powershell
rg --files src\MAF\Common\CanDoItAll.AgentFramework.Maf\Runtime | rg "MafAgentRuntime.*\.cs$"
```

```powershell
rg -n "private sealed class|private sealed partial class|private sealed record|private enum|internal sealed class|public sealed partial class" src\MAF\Common\CanDoItAll.AgentFramework.Maf\Runtime -g "MafAgentRuntime*.cs"
```

```powershell
rg -n "new (SkillCapabilityBuilder|ContextCapabilityBuilder|McpCapabilityBuilder|ToolCapabilityBuilder|WorkspaceRuntimePlugin)|RuntimeCapabilityComposition\(|CreateCapabilityComposition|CreateCapabilityStateCoreAsync|CreateRuntimeBuildAsync|RunCoreAsync|ExecuteRunAsync" src\MAF\Common\CanDoItAll.AgentFramework.Maf\Runtime -g "MafAgentRuntime*.cs"
```

```powershell
$files = Get-ChildItem -Path 'src\MAF\Common\CanDoItAll.AgentFramework.Maf\Runtime' -Recurse -Filter 'MafAgentRuntime*.cs' | Sort-Object FullName; foreach ($file in $files) { $lineCount = (Get-Content -Path $file.FullName | Measure-Object -Line).Lines; $relative = Resolve-Path -Path $file.FullName -Relative; "$relative|$lineCount" }
```

## Prior Bundle Context

- Previous bundle: `repo://codex/bundles/maf-runtime-architecture-isolation`
- Prior execution result: core seams extracted, but final closure explicitly marked partial.
