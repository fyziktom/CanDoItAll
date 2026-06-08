# Core Public API Owner Classification

## Snapshot
- Subbundle: `SB007`
- Assembly: `CanDoItAll.Processes.Core`
- Project: `repo://src/CanDoItAll.Processes.Core/CanDoItAll.Processes.Core.csproj`
- Public type count: `64`
- Surface hash: `99e2a6a6033d749f388a440360e4ef6db5b92c1d1fb2949a9f22d321ccd606d1`
- Compatibility level: `Descriptor/rule alpha`

## Dependency Boundary
- Owner: `Process Core`
- Allowed project reference: `CanDoItAll.Processes.Contracts`
- Denied references: driver abstractions, driver implementations, Modules, Infrastructure, AgentFramework, Entity Framework, storage/workspace services, runtime host/registry/selector surfaces.
- Runtime capability: none. This package owns deterministic read models and rules only.

## Owner Map
| Namespace | Owner | Classification | Compatibility |
| --- | --- | --- | --- |
| `CanDoItAll.Processes.Core.Artifacts` | `Process Core artifact descriptors and deterministic rules` | `Stable candidate read models/rules` | `Compatible additions allowed; renames/removals require migration doc and Gate N refresh.` |
| `CanDoItAll.Processes.Core.Diagnostics` | `Process Core retry diagnostics` | `Stable candidate diagnostic descriptors/rules` | `Compatible additions allowed; semantic changes require focused diagnostic tests.` |
| `CanDoItAll.Processes.Core.Execution` | `Process Core execution evidence descriptors` | `Stable candidate evidence descriptors/rules` | `Compatible additions allowed; execution side effects remain out of scope.` |
| `CanDoItAll.Processes.Core.Finalization` | `Process Core finalizer evidence descriptors` | `Stable candidate evidence descriptors/rules` | `Compatible additions allowed; finalizer mutation remains adapter-owned.` |
| `CanDoItAll.Processes.Core.Routing` | `Process Core dispatch route read models and rules` | `Stable candidate route rules` | `Compatible additions allowed; dispatcher mutation remains module-owned.` |
| `CanDoItAll.Processes.Core.Subprocess` | `Process Core subprocess lifecycle rules` | `Stable candidate lifecycle facts/rules` | `Compatible additions allowed; subprocess runtime orchestration remains outside Core.` |

## Public Types
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchDiagnostic`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatcher`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationMatchReason`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionDiagnostic`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionReason`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSatisfactionRules`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactExpectationSnapshot`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityDescriptor`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEligibilityRules`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionEvidenceDescriptorRules`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionLineageDescriptor`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactProjectionSourceOrderDescriptor`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordedSatisfactionRules`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactRecordSnapshot`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationPolicyRules`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptor`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationRequirementDescriptorRules`
- `CanDoItAll.Processes.Core.Artifacts.ProcessArtifactValidationSnapshot`
- `CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactExpectationMode`
- `CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactKind`
- `CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProducerKind`
- `CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactProjectionSourceKind`
- `CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustRequirement`
- `CanDoItAll.Processes.Core.Artifacts.ProcessCoreArtifactTrustStatus`
- `CanDoItAll.Processes.Core.Artifacts.ProcessCoreProviderNativeBrowserEvidenceKind`
- `CanDoItAll.Processes.Core.Artifacts.ProcessCoreSensitivityLevel`
- `CanDoItAll.Processes.Core.Artifacts.ProcessProviderNativeBrowserEvidenceDescriptor`
- `CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnostic`
- `CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceDiagnosticReason`
- `CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessArtifactSourceResolver`
- `CanDoItAll.Processes.Core.Artifacts.ProcessSubprocessOutputArtifactMapping`
- `CanDoItAll.Processes.Core.Diagnostics.ProcessNoProgressRetryDiagnosticDescriptor`
- `CanDoItAll.Processes.Core.Diagnostics.ProcessProviderRepairDiagnosticDescriptor`
- `CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptor`
- `CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticDescriptorRules`
- `CanDoItAll.Processes.Core.Diagnostics.ProcessRetryDiagnosticFailureKind`
- `CanDoItAll.Processes.Core.Execution.ProcessCoreExecutionRunObservationKind`
- `CanDoItAll.Processes.Core.Execution.ProcessExecutionAttemptEvidenceDescriptor`
- `CanDoItAll.Processes.Core.Execution.ProcessExecutionCarriedProofDescriptor`
- `CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptor`
- `CanDoItAll.Processes.Core.Execution.ProcessExecutionEvidenceDescriptorRules`
- `CanDoItAll.Processes.Core.Execution.ProcessExecutionRunEvidenceDescriptor`
- `CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerBlockCauseKind`
- `CanDoItAll.Processes.Core.Finalization.ProcessCoreFinalizerKind`
- `CanDoItAll.Processes.Core.Finalization.ProcessFinalizerEvidenceDescriptor`
- `CanDoItAll.Processes.Core.Finalization.ProcessFinalizerEvidenceDescriptorRules`
- `CanDoItAll.Processes.Core.Finalization.ProcessFinalizerIntentEvidenceDescriptor`
- `CanDoItAll.Processes.Core.Finalization.ProcessFinalizerResultEvidenceDescriptor`
- `CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecision`
- `CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDecisionReason`
- `CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteDiagnostic`
- `CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteEligibility`
- `CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteKind`
- `CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteOrderAssertion`
- `CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePipeline`
- `CanDoItAll.Processes.Core.Routing.ProcessDispatchRoutePlanner`
- `CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteSnapshot`
- `CanDoItAll.Processes.Core.Routing.ProcessDispatchRouteStage`
- `CanDoItAll.Processes.Core.Routing.ProcessDispatchStepTransitionIntent`
- `CanDoItAll.Processes.Core.Routing.ProcessDispatchTransitionIntentRules`
- `CanDoItAll.Processes.Core.Routing.ProcessDispatchTriggerFacts`
- `CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessLifecycleRules`
- `CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessParentTransitionFacts`
- `CanDoItAll.Processes.Core.Subprocess.ProcessSubprocessRunFacts`

## Reopen Triggers
- Reopen SB007/SB009 when the public type count or surface hash changes.
- Reopen SB007/SB009 when Core gains a driver, module, infrastructure, storage, workspace, EF, registry, selector, host, provider, DI, or manager-command dependency.
- Reopen SB007/SB009 when a Core descriptor starts performing I/O, storage writes, workspace writes, external calls, process mutation, finalizer mutation, transition application, claim mutation, or retry scheduling.
