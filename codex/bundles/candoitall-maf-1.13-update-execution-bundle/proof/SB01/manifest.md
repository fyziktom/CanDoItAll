# SB01 Proof Manifest

Status: `Completed`

Owned requirements: `RQ-001`, `RQ-002`

Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`

## Command Transcripts

| Purpose | Transcript |
| --- | --- |
| Branch and git state | `bundle://proof/SB01/transcripts/git-status.md` |
| SDK environment | `bundle://proof/SB01/transcripts/dotnet-info.md` |
| Direct package references | `bundle://proof/SB01/transcripts/package-search.md` |
| Package list | `bundle://proof/SB01/transcripts/package-list.md` |
| Package file hashes before update | `bundle://proof/SB01/transcripts/package-file-hashes-before.md` |
| Focused test candidate inventory | `bundle://proof/SB01/transcripts/test-discovery.md` |
| Playwright prerequisite | `bundle://proof/SB01/transcripts/playwright-prereq.md` |
| CodeAnalytics baseline | `bundle://proof/SB01/transcripts/codeanalytics-baseline.md` |
| Baseline restore | `bundle://proof/SB01/transcripts/baseline-restore.md` |
| Baseline build | `bundle://proof/SB01/transcripts/baseline-build.md` |
| No package edits during SB01 | `bundle://proof/SB01/transcripts/no-package-change-assertion.md` |

## Baseline Result

- Branch: `memory-providers`.
- Restore before package edits: passed.
- Full Release build before package edits: passed.
- Pre-existing warning: `Microsoft.OpenApi` 2.0.0 NU1903 high-severity vulnerability warning.
- CodeAnalytics snapshot: `snap-20260708002602-f2b77ff7`.
- Playwright prerequisite: `npx` is available.

## Changed-File Manifest

No production source or package files were changed in `SB01`.

| File | Before SHA-256 | After SHA-256 | Notes |
| --- | --- | --- | --- |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj` | `94184F94A489ADC321382D4E607FD97ADA92FCA68F56800B6A6F6BE8BC23A200` | `94184F94A489ADC321382D4E607FD97ADA92FCA68F56800B6A6F6BE8BC23A200` | Baseline only. |
| `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/CanDoItAll.AgentFramework.Workflows.MafAdapter.csproj` | `07B7C5D826D27D9823B0B0E702EEFC38BCEFB0DFE6E6D375A735589A7ACE478A` | `07B7C5D826D27D9823B0B0E702EEFC38BCEFB0DFE6E6D375A735589A7ACE478A` | Baseline only. |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Hosting/CanDoItAll.AgentFramework.Hosting.csproj` | `58512F9732F1C18DED8C7D17994F2CEF050110341F8971D3A4AB7230F51773B4` | `58512F9732F1C18DED8C7D17994F2CEF050110341F8971D3A4AB7230F51773B4` | Baseline only. |
| `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj` | `79803D75DC784B74FF69DF49A525E83253921753ECA68C59E16810DAAB90C198` | `79803D75DC784B74FF69DF49A525E83253921753ECA68C59E16810DAAB90C198` | Baseline only. |

## Source Assertions

- `SB01` did not edit package references or production source.
- Direct package references are limited to the intended MAF, Hosting, Workflows adapter, and Tooling projects.
- Baseline restore/build passed before package edits, so later compile errors are package-induced unless contradicted by a new unrelated change.

## Production Behavior Artifact Matrix

No new production signal, state, record, or event was introduced in `SB01`.
