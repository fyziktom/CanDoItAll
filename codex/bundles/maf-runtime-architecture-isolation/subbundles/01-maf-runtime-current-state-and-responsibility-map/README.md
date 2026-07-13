# 01-maf-runtime-current-state-and-responsibility-map

## Status

- `Ready`

## Objective

Create the source-backed responsibility map and baseline evidence that drives the whole MAF runtime refactor. Confirm the corrected scope excludes Financial Strategist/domain-specific work, then document current runtime responsibilities, testability pain points, and performance measurement baselines.

## Covered Inputs

- M001, M002, M003, M006, M008, M011
- R001, R002, R011, R012

## Prerequisites

- Repaired bundle is prepared.
- No production code changes before current-state proof is captured.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.RuntimeToolProviders.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Mcp.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Context.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/MafAgentRuntime.WorkspaceRuntimePlugin.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeToolProviderCompositionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentContextContributionTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentFinalizerPolicyTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/MafAgentRuntimeAttachmentTests.cs`

## Deliverables

- `proof/SB01/responsibility-map.md` mapping current methods/files to target owners.
- `proof/SB01/testability-baseline.md` listing reflection-heavy tests and full-runtime construction tests.
- `proof/SB01/performance-baseline-plan.md` defining measurement commands and timing boundaries for SB07.
- Scope audit proving agent-specific Financial Strategist/domain work is absent from this repaired bundle.
- Updated execution report.

## Dependency Impact

- SB02 depends on this map to define contracts around real seams instead of guessed abstractions.
- SB03-SB06 depend on this baseline to know which behavior and tests must move.
- SB07 depends on the performance baseline plan to prove impact honestly.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Inventory `MafAgentRuntime` partial files, nested classes, mutable state types, helper methods, and fallback construction sites.
2. Map each responsibility to target owner categories: runtime entry, build coordinator, capability composer, provider composer, provider/session/finalizer driver, feature driver, diagnostics, metrics.
3. Identify existing tests that use full runtime construction, private reflection, or nested private types for the mapped responsibilities.
4. Define baseline performance measurement points without changing code.
5. Run the scope audit to confirm Financial Strategist/domain-specific work is not part of this bundle.
6. Write proof artifacts and update the execution report.

## Scope Exceptions

- No implementation changes in this subbundle.
- Financial Strategist and MarkItDown remain deferred examples only.

## Do Not Do

- Do not extract code.
- Do not rename files as a substitute for responsibility mapping.
- Do not add Financial Strategist, margin, document, or project-structure work.
- Do not make performance claims without measurements.

## Acceptance Checklist

- [ ] Responsibility map covers the major runtime partials and nested builders.
- [ ] Testability baseline identifies reflection/full-runtime construction pain points.
- [ ] Performance baseline plan separates local runtime composition from external provider latency.
- [ ] Scope audit confirms domain-specific work is absent.
- [ ] Execution report is updated.

## Proof Required

- `proof/SB01/manifest.md`
- `proof/SB01/semantic-invariants.md`
- `proof/SB01/responsibility-map.md`
- `proof/SB01/testability-baseline.md`
- `proof/SB01/performance-baseline-plan.md`
- Semantic Adequacy Gate: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure.

## Browser Validation Logging

- N/A. Backend architecture preparation only.

## Progression Gate

- SB02 may start only after the responsibility map names current owners, target owners, test pain points, and measurement boundaries.

## Suggested Agent Prompt

```text
Implement SB01 only. Capture current MAF runtime responsibilities, reflection-heavy tests, and performance measurement boundaries without changing production code. Remove no code and do not drift into Financial Strategist/domain work.
```
