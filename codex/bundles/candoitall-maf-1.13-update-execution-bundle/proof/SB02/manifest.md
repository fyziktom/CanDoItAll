# SB02 Proof Manifest

Status: `Completed`

Owned requirements: `RQ-003`, `RQ-004`, `RQ-005`

Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`

## Command Transcripts

| Purpose | Transcript |
| --- | --- |
| Current NuGet outdated check | `bundle://proof/SB02/transcripts/outdated.md` |
| Initial package diff | `bundle://proof/SB02/transcripts/package-diff.md` |
| First restore attempt | `bundle://proof/SB02/transcripts/restore.md` |
| Dependency-floor decision | `bundle://proof/SB02/transcripts/dependency-floor.md` |
| Restore after floor changes | `bundle://proof/SB02/transcripts/restore-after-floor.md` |
| A2A preview decision | `bundle://proof/SB02/transcripts/a2a-decision.md` |
| Mem0 decision | `bundle://proof/SB02/transcripts/mem0-decision.md` |
| Package list after update | `bundle://proof/SB02/transcripts/package-list-after.md` |
| Stale stable MAF 1.8 scan | `bundle://proof/SB02/transcripts/stale-maf18-scan.md` |
| Anti-stub audit | `bundle://proof/SB02/transcripts/anti-stub.md` |
| Final package hashes | `bundle://proof/SB02/transcripts/package-file-hashes-final.md` |

## Package Decision Table

| Project | Package | Before | After | Reason |
| --- | --- | --- | --- | --- |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI` | `1.8.0` | `1.13.0` | Stable MAF target. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI.OpenAI` | `1.8.0` | `1.13.0` | Stable MAF target. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI.Workflows` | `1.8.0` | `1.13.0` | Stable MAF target. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI.A2A` | `1.8.0-preview.260528.1` | `1.13.0-preview.260703.1` | Current NuGet CLI preview. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `Microsoft.Agents.AI.Mem0` | `1.0.0-preview.251028.1` | `1.0.0-preview.251028.1` | Current NuGet CLI still reports latest as not found; do not guess. |
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj` | `Microsoft.Agents.AI` | `1.8.0` | `1.13.0` | Stable MAF target. |
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj` | `Microsoft.Agents.AI.Workflows` | `1.8.0` | `1.13.0` | Stable MAF target. |
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj` | `Microsoft.Extensions.AI.Abstractions` | `10.5.1` | `10.6.0` | NU1605 floor from MAF `1.13.0`. |
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj` | `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.7` | `10.0.9` | NU1605 floor from MAF `1.13.0`. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj` | `Microsoft.Agents.AI.Hosting.A2A` | `1.8.0-preview.260528.1` | `1.13.0-preview.260703.1` | Current NuGet CLI preview. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj` | `Microsoft.Extensions.DependencyInjection.Abstractions` | `10.0.7` | `10.0.9` | NU1605 floor from Hosting A2A preview. |
| `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj` | `Microsoft.Extensions.AI.Abstractions` | `10.5.1` | `10.5.1` | No restore floor required in this project. |

## Changed-File Manifest

| File | Before SHA-256 | After SHA-256 |
| --- | --- | --- |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `94184F94A489ADC321382D4E607FD97ADA92FCA68F56800B6A6F6BE8BC23A200` | `9F529E2B26FC48909BEF930EBACB7C89EF719DE062C24B79CE5C8FDB79C97634` |
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj` | `07B7C5D826D27D9823B0B0E702EEFC38BCEFB0DFE6E6D375A735589A7ACE478A` | `C2EDDC5DE56754F296A5BDFD35C69755699F49714D2696EDB3582CBB9536A301` |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj` | `58512F9732F1C18DED8C7D17994F2CEF050110341F8971D3A4AB7230F51773B4` | `2EFB842E39B65A00D55008C2C197B3436C7DDC8C9563C0ABF6B671817E991DC4` |
| `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj` | `79803D75DC784B74FF69DF49A525E83253921753ECA68C59E16810DAAB90C198` | `79803D75DC784B74FF69DF49A525E83253921753ECA68C59E16810DAAB90C198` |

## Source Assertions

- Only package references in targeted MAF adapter/hosting projects changed.
- No application source files were changed in `SB02`.
- Restore passes after dependency-floor versions proven by NU1605.
- Stable `Microsoft.Agents.AI` 1.8 references are absent from targeted project files after the update.

## Production Behavior Artifact Matrix

No new production signal, state, record, or event was introduced in `SB02`.
