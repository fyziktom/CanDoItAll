# NuGet Update Inventory

## Current direct package references found in source review

### `src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`

| Package | Current version | Phase-1 target | Rule |
| --- | ---: | ---: | --- |
| `Azure.AI.OpenAI` | `2.9.0-beta.1` | keep | Not a MAF package. Do not update unless restore reports an unavoidable conflict. |
| `Microsoft.Agents.AI` | `1.8.0` | `1.13.0` | Update. |
| `Microsoft.Agents.AI.A2A` | `1.8.0-preview.260528.1` | verify via NuGet CLI | Preview package. Update only if `dotnet list package --outdated --include-prerelease` reports a compatible 1.13-line package. Otherwise keep and fix only compile/restore issues. |
| `Microsoft.Agents.AI.Mem0` | `1.0.0-preview.251028.1` | verify via NuGet CLI | Preview package. Update only if NuGet CLI confirms a compatible package. Otherwise keep. |
| `Microsoft.Agents.AI.OpenAI` | `1.8.0` | `1.13.0` | Update. |
| `Microsoft.Agents.AI.Workflows` | `1.8.0` | `1.13.0` | Update. |
| `ModelContextProtocol` | `1.1.0` | keep | MAF 1.10 release notes mention internal bump to 1.2.0, but this repo direct reference should not be changed unless restore/compile requires it. |
| `OllamaSharp` | `5.4.25` | keep | Not part of MAF update. |
| `OpenTelemetry.Api` | `1.15.3` | keep | Compatible with MAF 1.13 dependency floor. |

### `src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj`

| Package | Current version | Phase-1 target | Rule |
| --- | ---: | ---: | --- |
| `Microsoft.Agents.AI` | `1.8.0` | `1.13.0` | Update. |
| `Microsoft.Agents.AI.Workflows` | `1.8.0` | `1.13.0` | Update. |
| `Microsoft.Extensions.AI.Abstractions` | `10.5.1` | `10.6.0` | Update to match MAF 1.13 dependency floor and avoid package downgrade warnings. |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.7` | `10.0.9` | Update to match MAF 1.13 dependency floor and avoid downgrade warnings. |

## Candidate patch

Use this as the first patch. Do not modify unrelated package versions in the same commit.

```xml
<!-- src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj -->
<PackageReference Include="Microsoft.Agents.AI" Version="1.13.0" />
<PackageReference Include="Microsoft.Agents.AI.OpenAI" Version="1.13.0" />
<PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.13.0" />
```

```xml
<!-- src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj -->
<PackageReference Include="Microsoft.Agents.AI" Version="1.13.0" />
<PackageReference Include="Microsoft.Agents.AI.Workflows" Version="1.13.0" />
<PackageReference Include="Microsoft.Extensions.AI.Abstractions" Version="10.6.0" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.9" />
```

## Preview package rule for A2A and Mem0

Do not guess preview package versions by hand. Run:

```powershell
dotnet list src\MAF\Common\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj package --outdated --include-prerelease
```

Then apply this decision table:

| CLI result | Action |
| --- | --- |
| Shows `Microsoft.Agents.AI.A2A` compatible update in the 1.13 line | Update A2A in the same package commit, then fix compile errors locally. |
| Shows only older or incompatible preview | Keep current A2A version. |
| Shows `Microsoft.Agents.AI.Mem0` compatible update | Update Mem0 in the same package commit, then fix compile errors locally. |
| Shows no compatible Mem0 update | Keep current Mem0 version. |
| Restore fails because preview package requires older MAF APIs | Isolate with a minimal adapter fix or temporarily disable only the direct preview integration surface behind existing feature flags. Do not remove memory/provider abstractions. |

## Restore and inventory commands

Run from repository root:

```powershell
dotnet --info
dotnet list src\MAF\Common\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj package
dotnet list src\MAF\Workflows\CanDoItAll.AgentFramework.Workflows.MafAdapter\CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj package
dotnet list src\MAF\Common\CanDoItAll.AgentFramework.Maf\CanDoItAll.AgentFramework.Maf.csproj package --outdated --include-prerelease
dotnet restore CanDoItAll.slnx
```

If `dotnet list CanDoItAll.slnx package` is unsupported for `.slnx`, run package listing per project path.

## Package management rule

The reviewed branch did not expose a root `Directory.Packages.props`; package versions are direct project references. Do not introduce central package management in phase 1 unless Codex finds an already-existing central package file in the working tree.
