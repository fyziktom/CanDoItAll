# 02-runtime-contracts-and-composition-root

## Status

- `Ready`

## Objective

Define the typed runtime contracts, dependency classification, options, and grouped DI registration strategy that make later extraction real. This subbundle creates the architecture skeleton before moving behavior.

## Covered Inputs

- M003, M004, M007, M009, M010
- R003, R007, R010, R012

## Prerequisites

- SB01 responsibility map and testability baseline.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderRuntimeGateway.cs`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`

## Deliverables

- Typed contract plan for runtime build, capability composition, provider composition, diagnostics, measurement, and dependency defaults.
- Dependency classification table: required, optional, defaulted, legacy adapter.
- Grouped service registration plan such as `AddCanDoItAllMafRuntime`, `AddCanDoItAllMafCapabilityComposition`, and `AddCanDoItAllMafProviderRuntime`.
- Tests or planned tests for missing required services and default registration behavior.
- Updated execution report.

## Dependency Impact

- SB03-SB05 must use these contracts instead of inventing phase-local abstractions.
- SB06 depends on these contracts for fake/mock test harness boundaries.
- SB07 depends on measurement contracts and dependency classification for closure checks.

## Validation Depth

- `Critical architecture foundation`

## Implementation Steps

1. Review SB01 responsibility map and define the minimal set of collaborator contracts.
2. Classify every fallback construction in current runtime files as required, optional, defaulted, or legacy compatibility.
3. Define request/result records for runtime build, capability composition, provider composition, diagnostics, and measurements.
4. Define options classes and validation points for fallback policy, diagnostics, and measurement behavior.
5. Define grouped registration methods and duplicate-safe extension-point registration rules.
6. Add architecture/contract tests if implementation begins in this subbundle.
7. Update proof and execution report.

## Scope Exceptions

- This subbundle should not move large behavior bodies yet unless required to compile the contracts.
- It does not implement feature-driver extraction.

## Do Not Do

- Do not create interfaces for every private method.
- Do not preserve raw `IServiceProvider.GetService` as the default pattern in new drivers.
- Do not remove all fallbacks without classifying host/test impact.
- Do not add agent-specific domain contracts.

## Acceptance Checklist

- [ ] Contracts match SB01 real seams.
- [ ] Dependency classifications are explicit.
- [ ] Registration strategy is grouped and testable.
- [ ] Missing required dependencies have planned explicit failures.
- [ ] Later subbundles can consume the same contracts.

## Proof Required

- `proof/SB02/manifest.md`
- `proof/SB02/semantic-invariants.md`
- `## Production Behavior Artifact Matrix` for new contracts/options/diagnostics.
- Contract and registration test output if code changes are made.
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Browser Validation Logging

- N/A. Backend architecture contracts only.

## Progression Gate

- SB03-SB05 may start only after the runtime contracts and dependency classifications are stable enough to prevent phase-local abstraction drift.

## Suggested Agent Prompt

```text
Implement SB02 only. Define typed MAF runtime contracts, dependency classifications, options, and grouped registrations around the SB01 responsibility map. Do not move feature behavior or add domain-specific agent work.
```
