# SB08 MAF Reconnection And Compatibility

## Status

- Result: `Passed with compatibility-adapter risk`
- Validation depth: `Critical runtime reconnection`
- Browser validation: `N/A`
- UI viewport validation: `Skipped; SB08 has no UI surface and the app is large-screen only`
- Next gate: `SB09 may start`

## Implementation Summary

- Moved `ProcessAllowedOperationsCapabilityPolicyCompiler` from Persistence seed code into `CanDoItAll.AgentFramework.Core` so runtime and seed paths share the typed process-operation policy compiler.
- Added a MAF runtime capability access plan that builds candidate descriptors once, evaluates them through `ICapabilityAccessPolicyEvaluator`, and exposes an `EffectiveCapabilitySet` on runtime state.
- Routed catalog Tool, Skill, and MCP server descriptors through the isolated `ToolDescriptorFactory`, `SkillDescriptorFactory`, `McpDescriptorFactory`, and exposure descriptor factories before access evaluation.
- Routed configured workspace/storage tools and registered runtime-provider tools through the same access policy plan; provider-created tools now append structured suppression diagnostics to runtime state.
- Removed active MAF process-step private suppression paths for skills, browser MCP filtering, process-scoped workspace access, and runtime-provider context filtering.
- Preserved existing concrete MAF object builders as compatibility adapters for framework-native `AITool`, `AgentSkill`, and MCP attachment.

## Evidence

| Evidence | Path |
| --- | --- |
| Failing-first descriptor factory invariant | `proof/SB08/transcripts/failing-first-descriptor-factory-test.txt` |
| Passing descriptor factory invariant | `proof/SB08/transcripts/passing-descriptor-factory-test.txt` |
| Passing SB08 focused MAF access tests | `proof/SB08/transcripts/passing-maf-access-tests.txt` |
| Full MAF tool/provider composition regression | `proof/SB08/transcripts/regression-maf-tool-provider-composition-tests.txt` |
| Template seed regression | `proof/SB08/transcripts/regression-template-seed-tests.txt` |
| Execution capability-filtering integration regression | `proof/SB08/transcripts/regression-capability-filtering-integration-tests.txt` |
| Solution build | `proof/SB08/transcripts/dotnet-build-solution.txt` |
| Source assertions | `proof/SB08/transcripts/source-assertions.txt` |
| Anti-stub audit | `proof/SB08/transcripts/anti-stub-audit.txt` |
| Static performance scan | `proof/SB08/transcripts/static-performance-scan.txt` |
| File-size scan | `proof/SB08/transcripts/file-size-scan.txt` |
| Changed file hashes | `proof/SB08/changed-file-hashes.txt` |

## Test Commands

```text
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "SB08_INV_MAF_ACCESS"
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~MafAgentRuntimeToolProviderCompositionTests"
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~CapabilityTemplateSeedMaterializationTests|FullyQualifiedName~CapabilityTemplateSeedHardeningCheckpointTests"
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~AgentFrameworkExecutionCapabilityFilteringIntegrationTests"
dotnet build CanDoItAll.slnx --no-restore
```

## Results

- SB08 focused MAF access tests: `3 passed`
- Full MAF tool/provider composition regression: `27 passed`
- Template seed regression: `13 passed`
- Execution capability-filtering integration regression: `6 passed`
- Solution build: `0 warnings`, `0 errors`

## Accepted Risks

| Risk | Decision | Follow-up |
| --- | --- | --- |
| MAF still contains framework-native Skill/Tool/MCP construction DTOs and switch-based object builders for concrete `AITool`, `AgentSkill`, and MCP client attachment. | Accepted for SB08 because the active access decision path no longer depends on those local suppression branches; candidate descriptors now come from isolated descriptor factories and all process/runtime filtering flows through the shared evaluator. | SB10/SB12 should split concrete MAF adapters out of large partial files and remove DTOs once isolated services expose framework-native adapters. |
| `MafAgentRuntime.Capabilities.Access.cs` is 999 lines after adding descriptor adapters. | Accepted to keep the runtime access migration localized and avoid a broad refactor during the compatibility subbundle. | Split access-plan construction and descriptor adapters into smaller files during cleanup. |

## Progression Decision

- `SB08 completed; SB09 unblocked.`
