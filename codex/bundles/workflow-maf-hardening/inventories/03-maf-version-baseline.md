# MAF Version Baseline

SB01 inventory date: 2026-05-28.

Proof transcripts:

- `bundle://proof/SB01/transcripts/package-scan.txt`
- `bundle://proof/SB01/transcripts/nuget-version-scan.txt`
- `bundle://proof/SB01/transcripts/restore-build.txt`

## Local Package References

| Project | Package | Local version | NuGet latest observed | Decision |
| --- | --- | --- | --- | --- |
| `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI` | `1.6.2` | `1.8.0` | Stay on `1.6.2` for this bundle. |
| `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI.A2A` | `1.6.2-preview.260521.1` | `1.8.0-preview.260528.1` | Stay on current preview for this bundle. |
| `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI.Mem0` | `1.0.0-preview.251028.1` | `1.0.0-preview.251028.1` | Already current. |
| `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI.OpenAI` | `1.6.2` | `1.8.0` | Stay on `1.6.2` for this bundle. |
| `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI.Workflows` | `1.6.2` | `1.8.0` from NuGet flat-container API; public gallery page observed as `1.7.0` during browser check. | Stay on `1.6.2` for this bundle. |
| `repo://src/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj` | `Microsoft.Agents.AI.Hosting.A2A` | `1.6.2-preview.260521.1` | `1.8.0-preview.260528.1` | Stay on current preview for this bundle. |

## Upgrade Decision

- Decision: remain on the currently restored `1.6.2` stable MAF package line and matching `1.6.2-preview.260521.1` A2A hosting packages for this hardening bundle.
- Reasoning: the repo already restores and builds cleanly on the current package line, the workflow APIs needed by this bundle are already present, and jumping to `1.8.0` would add a separate package/API migration risk to an already broad runtime hardening bundle.
- Scope boundary: SB03 and SB05 must harden the local adapter, event, and executor behavior against the current MAF APIs. A later package-upgrade bundle can move to `1.8.0` after this runtime baseline has stronger tests.
- Stale bundle correction: the prepared bundle mentioned `1.7.0`, but NuGet flat-container metadata on 2026-05-28 listed newer `1.8.0` versions for the stable `Microsoft.Agents.AI`, `Microsoft.Agents.AI.OpenAI`, and `Microsoft.Agents.AI.Workflows` packages.

## Build Baseline

- `dotnet restore CanDoItAll.slnx`: passed.
- `dotnet build CanDoItAll.slnx --no-restore`: passed.
- Noted warnings: existing MSB3277 conflicts between `Microsoft.EntityFrameworkCore.Relational` `10.0.0.0` and `10.0.4.0` across several projects. This is outside the MAF hardening scope but should be tracked separately.
