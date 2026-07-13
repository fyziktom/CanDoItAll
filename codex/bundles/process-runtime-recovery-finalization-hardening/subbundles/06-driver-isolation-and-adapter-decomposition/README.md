# Driver Isolation And Adapter Decomposition

## Status

- `Completed`

## Objective

Isolate domain-specific process policy behind driver contracts and decompose broad AgentFramework/runtime partial clusters so finalization, evidence, context packaging, and recovery classification are testable without polluting the generic runtime.

## Covered Inputs

- R11, R12, R13, R14
- US08, US10
- EX05, EX06, EX07, EX10, EX13, EX16
- Architect notes about keeping runtime generic, using process drivers for domain-specific code, and avoiding partial-class pseudo-isolation.

## Prerequisites

- SB02 through SB05 progression gates passed, or this subbundle is explicitly split into a preparatory adapter extraction that does not change behavior.
- Generic contracts for lineage, retrieval, finalization, and recovery routes are stable.

## Exact Source References

- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifactEvidence.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionParsing.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ResultConversion.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessStepBriefBuilder.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Rework.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Results.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`

## Deliverables

- Explicit driver abstractions for domain-specific evidence, finalization policy, context packaging, and recovery classification where needed.
- Extracted cohesive services from AgentFramework adapter partial responsibilities.
- Runtime recovery/finalization logic no longer relies on adapter-specific safe-retry heuristics.
- Source assertions protecting generic runtime boundaries.
- Unit tests for extracted services and adapter conversion.
- DI registration updates with clear ownership.

## Dependency Impact

- SB08 architecture closure depends on this subbundle.
- Without it, the code may pass behavior tests while keeping the untestable partial-class structure that caused the original maintainability risk.

## Validation Depth

- `Critical architecture closure`

## Implementation Steps

1. Inventory responsibilities still inside runtime and adapter partial clusters after SB02 through SB05.
2. Choose extraction targets that reduce real responsibility pressure.
3. Add only driver abstractions that represent concrete domain-specific decisions.
4. Move adapter-specific evidence/materialization/completion parsing into focused services.
5. Keep generic contracts in Core/Runtime/Driver Abstractions and AgentFramework specifics in Module integration.
6. Update DI registrations and tests.
7. Add source assertions for dependency direction and partial-cluster policy.
8. Update proof manifest and execution report.

## Scope Exceptions

- Does not require complete elimination of every partial file in one subbundle.
- Does not create interfaces for trivial single-use helpers unless needed for testing or true boundaries.
- Does not rewrite all drivers/templates.

## Do Not Do

- Do not add new final partial files to the large clusters.
- Do not create broad generic driver hooks that accept string commands or opaque dictionaries.
- Do not move AgentFramework concepts into Runtime.
- Do not extract services only to rename methods without improving testability.

## Acceptance Checklist

- At least the responsibilities touched by SB02 through SB05 are owned by cohesive services or explicit contracts.
- Runtime does not reference AgentFramework/MAF/Module/domain-specific packages.
- Driver-specific policy is isolated and unit-testable.
- Adapter conversion/materialization/finalization evidence behavior has focused tests.
- Source assertions prevent regression.

## Proof Required

- `bundle://proof/SB06/manifest.md` with changed-file hashes, commands, dependency proof, and source assertions.
- `bundle://proof/SB06/semantic-invariants.md` describing boundary and policy-isolation invariants.
- CodeAnalytics dependency refresh or equivalent graph proof.
- Unit test transcripts for extracted services.
- Source assertions for no runtime domain leakage and no final partial-class expansion.
- Anti-stub audit showing tests execute extracted production services.

## Browser Validation Logging

- Route: `N/A unless host UI or process management UI is touched`
- Viewports: if UI touched, large desktop plus affected responsive width
- Playwright evidence: required only if UI changed
- Screenshots: record concrete paths if UI changed
- Review questions: no visual regression in process management surfaces if touched.

## Progression Gate

- SB08 may proceed only when architecture assertions pass.
- Touched partial-cluster responsibilities must be testable through extracted services or explicit contracts.

## C# Architecture Impact

This is the main architecture refactor subbundle. It must improve maintainability and testability without adding unnecessary layers.

## Boundary Ownership

Driver abstractions own extension points. Standard drivers own generic policies. Module integration owns AgentFramework-specific services. Runtime owns generic decisions only.

## Dependency Direction

Preserve acyclic graph. Runtime remains free of Module/AgentFramework/MAF/UI/domain-specific references.

## Pattern Decision

Use small cohesive services and explicit policy contracts. Do not use inheritance hierarchies or service locators when composition/delegates/records are clearer.

## Testability Contract

Every extracted responsibility must have a direct unit test or be covered by a focused integration test with a clear reason.

## Partial Class Policy

No final partial expansion. Any temporary partial edit must include a removal/extraction note in the execution report.

## Architecture Proof Required

- CodeAnalytics dependency proof.
- Source assertions.
- Test transcripts.
- Responsibility movement notes.
- Partial-class policy audit.

## Suggested Agent Prompt

```text
Implement SB06 only. Isolate domain-specific process policy behind driver contracts and decompose touched partial-cluster responsibilities into cohesive testable services. Keep runtime generic and prove dependency direction with source assertions.
```
