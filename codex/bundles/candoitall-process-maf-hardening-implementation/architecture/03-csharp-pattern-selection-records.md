# C# Pattern Selection Records

## PSR-01: Blocked Step Packet Builder

## Context

Operator action and rework text currently assemble diagnostics inline from optional AgentFramework observations and runtime receipts.

## Forces

- extension growth: more blocker categories are needed.
- multiple implementations: one production builder and test fixture builders are useful.
- construction complexity: moderate; needs step, assignment, receipt, observation, artifact descriptors, child state, and tool preflight facts.
- external SDK isolation: none.
- runtime selection: no.
- testability: high.
- dependency direction: application/projection should consume runtime facts without module dependencies.

## Selected pattern

Builder.

## Rejected alternatives

- simpler class: acceptable only if it remains focused; still represented as builder behavior.
- partial class: rejected because it would grow `ProcessRuntimeProjectionQueryService`.
- switch statement: rejected for many future blocker categories.
- service locator: rejected.
- direct construction: rejected because tests need stable seam and focused packet assertions.

## New types and projects

| Type | Project | Responsibility |
| --- | --- | --- |
| `ProcessBlockedStepPacket` | `Processes.Application` or `Processes.Projections` | Structured projection/rework packet. |
| `IProcessBlockedStepPacketBuilder` | `Processes.Application` | Build packet from runtime and observation facts. |

## Test plan

| Test | Behavior proven |
| --- | --- |
| `OperatorAction_WhenAgentFrameworkObservationMissing_UsesRuntimeReceiptDiagnostics` | Runtime receipts still produce actionable packet. |
| `OperatorAction_DoesNotRecommendBlindRetry_WhenDiagnosticMissing` | No generic blind retry when state lacks diagnostic. |

## Proof that this is not fake separation

Source assertion must show packet category logic is outside `ProcessRuntimeProjectionQueryService`, with unit tests targeting the builder directly.

## PSR-02: Runtime-Owned Parent Subprocess Bridge

## Context

The adapter currently can launch/defer subprocesses but parent evidence resolution is generic and templates also instruct agents to launch child processes.

## Forces

- extension growth: multiple parent/child process contracts.
- multiple implementations: production bridge plus fake child-state provider in tests.
- construction complexity: child run lookup, artifact validation, managed artifact synthesis.
- external SDK isolation: project-structure/AgentFramework integration should stay outside runtime.
- runtime selection: contract launch mode selects runtime-owned vs agent-owned fallback.
- testability: high.
- dependency direction: runtime/application should call abstraction, module integration should implement I/O.

## Selected pattern

Strategy plus Builder.

## Rejected alternatives

- simpler class: insufficient because bridge result cases and launch modes vary.
- partial class: rejected as final boundary.
- switch statement: allowed only inside a focused result mapper, not in old adapter.
- service locator: rejected.
- direct construction: rejected because tests need fake child run/artifact provider.

## New types and projects

| Type | Project | Responsibility |
| --- | --- | --- |
| `IParentSubprocessArtifactBridge` | Drivers abstractions or application abstraction | Contract for bridge request/result. |
| `ParentSubprocessArtifactBridge` | `Modules.Processes` | Runtime-owned child state inspection and parent artifact synthesis. |
| `ParentSubprocessBridgeResult` | Contract project | Discriminated result cases. |
| `SubprocessContract` | Contracts/templates boundary | Accepted/no-go output definitions. |

## Test plan

| Test | Behavior proven |
| --- | --- |
| `PrepareSolutionSkeleton_WhenChildCompletedWithSetupHandoff_WritesParentEvidenceAndCompletes` | Accepted child handoff creates parent proof. |
| `PrepareSolutionSkeleton_WhenChildRepairEscalation_PropagatesConcreteNoGoBlocker` | No-go child blocks parent concretely. |
| `Adapter_RuntimeOwnedSubprocess_DoesNotInvokeExecuteRunAsync` | Normal agent not used for controlled subprocess handling. |

## Proof that this is not fake separation

Bridge tests must instantiate bridge with fake child/artifact providers, not the full adapter or app host. Old adapter should call the bridge and remain thin.

## PSR-03: Artifact Descriptor Resolver And Materializer

## Context

Runtime prompts and produced artifacts are slot-centric and output-hash-centric.

## Forces

- extension growth: descriptors must support normal outputs, child mappings, recovered existing proof, and runtime-synthesized handoffs.
- multiple implementations: descriptor resolver, managed artifact materializer, tests with fake materializer.
- construction complexity: combines template expectations, assignment slots, managed ref conventions, readback hash.
- external SDK isolation: file I/O belongs in module integration.
- runtime selection: materialization mode drives behavior.
- testability: high.
- dependency direction: contracts stay low; I/O stays module-level.

## Selected pattern

Adapter plus Builder.

## Rejected alternatives

- simpler class: acceptable for pure descriptor formatting only; not for I/O materialization.
- partial class: rejected.
- switch statement: acceptable only over a typed enum in a focused component.
- service locator: rejected.
- direct construction: rejected for materializer tests.

## New types and projects

| Type | Project | Responsibility |
| --- | --- | --- |
| `ProcessArtifactSlotDescriptor` | Drivers abstractions/contracts | Semantic artifact description. |
| `SubprocessArtifactMappingDescriptor` | Drivers abstractions/contracts | Accepted/no-go child mapping descriptor. |
| `IProcessArtifactDescriptorResolver` | Application or drivers abstractions | Resolve descriptors from template/assignment. |
| `IManagedArtifactMaterializer` | Module integration contract | Read/write/hash managed artifacts. |

## Test plan

| Test | Behavior proven |
| --- | --- |
| `RuntimeContractPrompt_IncludesArtifactKeysTitlesAndPrimaryManagedRefs` | Prompt is semantic. |
| `ProducedArtifactRef_UsesManagedArtifactContentHash` | Artifact hash reflects actual managed content. |
| `Finalization_WhenOutputMissing_DowngradesAndDoesNotLedgerInvalidProducedArtifact` | Ledger uses applied result. |

## Proof that this is not fake separation

Tests must fail if descriptor output reverts to GUID-only slot text or artifact hashes ignore managed content.

## PSR-04: Exact Runtime Tool Preflight

## Context

Readiness checks inspect agent capability metadata but actual tool availability depends on composed providers and governed process context.

## Forces

- extension growth: workspace, browser, project-structure, finalizer, and process tools.
- multiple implementations: production provider inspection plus fake catalog in tests.
- construction complexity: selected agent, runtime context, required tools, allowed operations, provider composition.
- external SDK isolation: provider composition details stay module/MAF-facing.
- runtime selection: preflight result controls dispatch.
- testability: high.
- dependency direction: dispatch depends on preflight abstraction, implementation can depend on provider catalog.

## Selected pattern

Strategy.

## Rejected alternatives

- simpler class: acceptable only behind interface if one implementation remains focused.
- partial class: rejected.
- switch statement: acceptable only over typed denial categories in a focused component.
- service locator: rejected.
- direct construction: rejected because dispatch tests need fake preflight.

## New types and projects

| Type | Project | Responsibility |
| --- | --- | --- |
| `IProcessRuntimeToolPreflightService` | Application/abstractions | Check required tool availability. |
| `ProcessRuntimeToolPreflightResult` | Contracts/abstractions | Typed pass/fail/denial details. |
| `ProcessRuntimeToolRequirement` | Contracts/abstractions | Required tool and source metadata. |

## Test plan

| Test | Behavior proven |
| --- | --- |
| `Dispatch_WhenProjectStructureLaunchToolNotComposed_BlocksBeforeAgentRun` | Missing composed tool prevents LLM execution. |
| `Dispatch_WhenToolDenied_ShowsDeniedDiagnosticWithScope` | Denial category and scope are actionable. |

## Proof that this is not fake separation

Dispatch proof must show `ExecuteRunAsync` is not invoked on mandatory preflight failure.
