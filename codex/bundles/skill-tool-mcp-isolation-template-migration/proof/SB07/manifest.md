# SB07 Template Seed Hardening Checkpoint

## Status

- Result: `Passed`
- Validation depth: `Mandatory hardening checkpoint`
- Browser validation: `N/A`
- Next gate: `SB08 may start`

## Implementation Summary

- Added SB07 hardening tests in `tests/CanDoItAll.Tests.Unit/CapabilityTemplateSeedHardeningCheckpointTests.cs`.
- Added an injectable capability-template pack root to `SandboxWorkspaceSeedBuilder.Build(...)` for invalid-pack no-fallback proof.
- Added typed `CapabilityAccessDefaultEffect` validation and rejected rule-level `inherit`.
- Hardened access-policy selector reference validation for capability keys, runtime tool names, MCP server keys, MCP tool names, and implementation keys.
- Changed process `AllowedOperations` compilation to emit behavior-preserving deny rules for restricted operation classifications not granted by the step contract.
- Added `AgentWorkspaceToolAccessCapabilityPolicyCompiler` so coarse agent workspace-tool flags compile to typed runtime-tool deny policy.
- Reworked policy-reference lookup building into a single pass over the template catalog.

## Evidence

| Evidence | Path |
| --- | --- |
| Failing-first transcript | `proof/SB07/transcripts/failing-first-template-seed-hardening-tests.txt` |
| Passing SB07 focused tests | `proof/SB07/transcripts/passing-template-seed-hardening-tests.txt` |
| SB06/SB07 template regression | `proof/SB07/transcripts/regression-template-seed-tests.txt` |
| SB01-SB07 focused regression | `proof/SB07/transcripts/regression-capability-contracts-through-sb07.txt` |
| Solution build | `proof/SB07/transcripts/dotnet-build-solution.txt` |
| Source assertions | `proof/SB07/transcripts/source-assertions.txt` |
| Anti-stub audit | `proof/SB07/transcripts/anti-stub-audit.txt` |
| Static performance scan | `proof/SB07/transcripts/static-performance-scan.txt` |
| File-size scan | `proof/SB07/transcripts/file-size-scan.txt` |
| Parity and dry-run report | `proof/SB07/parity-and-dry-run-report.md` |

## Test Commands

```text
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter FullyQualifiedName~CapabilityTemplateSeedHardeningCheckpointTests --no-restore
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~CapabilityTemplateSeedMaterializationTests|FullyQualifiedName~CapabilityTemplateSeedHardeningCheckpointTests" --no-restore
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~CapabilityContractsTests|FullyQualifiedName~ToolImplementationContractsTests|FullyQualifiedName~SkillLoaderContractsTests|FullyQualifiedName~McpRuntimeContractsTests|FullyQualifiedName~CapabilityFoundationHardeningTests|FullyQualifiedName~CapabilityTemplateSeedMaterializationTests|FullyQualifiedName~CapabilityTemplateSeedHardeningCheckpointTests" --no-restore
dotnet build CanDoItAll.slnx --no-restore
```

## Results

- SB07 focused tests: `6 passed`
- SB06/SB07 template tests: `13 passed`
- SB01-SB07 focused regression: `67 passed`
- Solution build: `0 warnings`, `0 errors`

## Accepted Risks

| Risk | Decision | Follow-up |
| --- | --- | --- |
| `SandboxWorkspaceSeedBuilder.cs` remains over 500 lines at 759 lines. | Accepted for SB07 because SB06 already removed active hardcoded capability construction; SB07 source assertions show the remaining old helper methods are definitions only, not active fallback paths. | SB11 cleanup should delete or split remaining inactive seed helper code after runtime reconnection proof stabilizes. |
| Template loader and materializer use synchronous local file reads. | Accepted for seed/template startup materialization. The loader is lazy, cached per instance, and not used per tool call. | Revisit only if setup UI later reloads packs repeatedly in SB10/SB11. |

## Progression Decision

- `SB07 completed; SB08 unblocked.`
