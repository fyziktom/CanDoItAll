# SB09 Runtime Hardening And Optimization Checkpoint

## Status

- Result: `Passed with accepted legacy-MAF size risk`
- Validation depth: `Mandatory runtime hardening checkpoint`
- Browser validation: `N/A`
- UI viewport validation: `Skipped; SB09 has no UI surface and the app is large-screen only`
- Next gate: `SB10 and SB11 may start`

## Implementation Summary

- Split the SB08 runtime access adapter into focused partial files for access orchestration, policy construction, configured/runtime tool descriptors, and catalog descriptors.
- Removed the redundant configured workspace-tool attach gate keyed directly from `AgentRuntimeContextIntent.WorkspaceToolsEnabled`; configured workspace tools now rely on the same `RuntimeCapabilityAccessPlan` and `EffectiveCapabilitySet` descriptors that record suppression diagnostics.
- Split configured workspace/tool-plugin attachment methods out of `MafAgentRuntime.Capabilities.Tools.cs` into `MafAgentRuntime.Capabilities.Tools.ConfiguredWorkspace.cs`, reducing the original tool adapter from 539 to 397 lines.
- Kept runtime-provider tool filtering, configured workspace/storage tools, and catalog Skill/Tool/MCP capabilities on the shared access evaluator path.
- Preserved structured diagnostics across template validation, internal tool, external process, skill loader, MCP setup/list-tools, timeout, cancellation, and cleanup contract tests.

## Evidence

| Evidence | Path |
| --- | --- |
| Runtime diagnostics and failure contract tests | `proof/SB09/transcripts/runtime-diagnostics-contract-tests.txt` |
| MAF runtime composition tests | `proof/SB09/transcripts/runtime-composition-tests.txt` |
| Process/runtime capability filtering integration tests | `proof/SB09/transcripts/runtime-capability-filtering-integration-tests.txt` |
| MAF project build after tool split | `proof/SB09/transcripts/maf-project-build-after-tool-split.txt` |
| Solution build | `proof/SB09/transcripts/dotnet-build-solution.txt` |
| Hidden-filter static search | `proof/SB09/transcripts/hidden-filter-static-search.txt` |
| Focused performance scan | `proof/SB09/transcripts/focused-performance-scan.txt` |
| File-size scan | `proof/SB09/transcripts/file-size-scan.txt` |
| Source assertions | `proof/SB09/transcripts/source-assertions.txt` |
| Anti-stub audit | `proof/SB09/transcripts/anti-stub-audit.txt` |
| Codeanalytics summary | `proof/SB09/codeanalytics-dependency-summary.md` |
| Runtime hardening report | `proof/SB09/runtime-hardening-report.md` |
| Changed file hashes | `proof/SB09/changed-file-hashes.txt` |

## Test Commands

```text
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CapabilityFoundationHardeningTests|FullyQualifiedName~ToolImplementationContractsTests|FullyQualifiedName~SkillLoaderContractsTests|FullyQualifiedName~McpRuntimeContractsTests|FullyQualifiedName~CapabilityTemplateSeedHardeningCheckpointTests"
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~MafAgentRuntimeToolProviderCompositionTests"
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~AgentFrameworkExecutionCapabilityFilteringIntegrationTests"
dotnet build src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj --no-restore
dotnet build CanDoItAll.slnx --no-restore
```

## Results

- Runtime diagnostics and failure contract tests: `46 passed`
- MAF runtime composition tests: `27 passed`
- Execution capability-filtering integration tests: `6 passed`
- MAF project build after split: `0 warnings`, `0 errors`
- Solution build: `0 warnings`, `0 errors`
- Codeanalytics snapshot: `snap-20260628165911-672062eb`
- Codeanalytics dependency query: `0 scoped cycles`

## Accepted Risks

| Risk | Decision | Follow-up |
| --- | --- | --- |
| `MafAgentRuntime.Capabilities.cs` remains 957 lines. | Accepted for SB09 because it is an existing orchestrator file, not new reconnection growth; SB09 kept new access/tool split files below 500 lines. | SB12 should split orchestration helpers once UI/API proof no longer depends on runtime movement. |
| `MafAgentRuntime.Capabilities.Mcp.cs` remains 651 lines. | Accepted for SB09 because it is already capability-kind scoped and still contains framework-native MCP lifecycle details required by the current MAF bridge. | SB12 should extract MCP hosted/local/http adapters when isolated MCP services expose MAF-native attachment helpers. |
| MCP child `allowedTools` filtering still happens after `ListToolsAsync` in the MAF MCP builder. | Accepted as a tested compatibility shim: MCP child tools are runtime-discovered and are not yet independent catalog candidates. Server-level MCP capability descriptors are evaluated before attachment. | SB11/SB12 should decide whether discovered MCP child tools become first-class effective-set candidates. |
| Focused performance scan still flags existing synchronous reads/process stream usage outside the new access split. | Accepted because these hits are pre-existing Core/AgentFactory/workspace helpers and not per-call parsing or sync-over-async introduced by SB09. | Address under a dedicated workspace/process-host hardening task, not this isolation checkpoint. |

## Progression Decision

- `SB09 completed; SB10 and SB11 unblocked.`
