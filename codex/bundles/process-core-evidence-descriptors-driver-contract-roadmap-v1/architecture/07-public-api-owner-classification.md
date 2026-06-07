# Public API Owner Classification

## Scope
- Classifies the public `CanDoItAll.Processes.Core` surface after SB004-SB021.
- The exact machine-readable API list is guarded in `Process_core_public_api_surface_is_explicitly_guarded` and recorded at `bundle://proof/SB015/transcripts/current-core-public-api-surface-after-projection-evidence.txt`.
- This document assigns ownership intent for review and future extraction decisions; it is not a runtime contract.

## Artifacts
| Public Surface Family | Owner Classification | Notes |
| --- | --- | --- |
| `ProcessCoreArtifactKind`, `ProcessCoreArtifactTrustRequirement`, `ProcessCoreArtifactTrustStatus`, `ProcessCoreSensitivityLevel` | Stable pure value model | Contract-neutral artifact value vocabulary. |
| `ProcessArtifactExpectationSnapshot`, `ProcessArtifactRecordSnapshot` | Stable pure read snapshot | Snapshot-only; no module entity dependency. |
| `ProcessArtifactExpectationMatcher`, `ProcessArtifactExpectationSatisfactionRules`, `ProcessArtifactRecordedSatisfactionRules` | Stable pure rule family | Deterministic matching/satisfaction only. |
| `ProcessSubprocessArtifactSourceResolver`, `ProcessSubprocessOutputArtifactMapping`, `ProcessSubprocessArtifactSourceDiagnostic` | Stable pure rule family | Subprocess artifact mapping and diagnostics only. |
| `ProcessCoreArtifactProjectionSourceKind`, `ProcessCoreArtifactProducerKind`, `ProcessCoreArtifactExpectationMode` | Stable descriptor vocabulary | Projection/validation value vocabulary. |
| `ProcessArtifactProjectionEligibilityRules`, `ProcessArtifactValidationRequirementDescriptorRules`, `ProcessArtifactValidationPolicyRules` | Stable descriptor rule family | Projection eligibility and validation policy only; no storage or path orchestration. |
| `ProcessArtifactProjectionEvidenceDescriptorRules`, `ProcessArtifactProjectionLineageDescriptor`, `ProcessArtifactProjectionSourceOrderDescriptor`, `ProcessProviderNativeBrowserEvidenceDescriptor`, `ProcessCoreProviderNativeBrowserEvidenceKind` | Stable descriptor rule family | Projection source order, lineage facts, and provider-native browser evidence descriptors only; module owns orchestration and output probing. |

## Execution
| Public Surface Family | Owner Classification | Notes |
| --- | --- | --- |
| `ProcessExecutionEvidenceDescriptorRules`, `ProcessExecutionEvidenceDescriptor`, `ProcessExecutionRunEvidenceDescriptor`, `ProcessExecutionAttemptEvidenceDescriptor`, `ProcessExecutionCarriedProofDescriptor`, `ProcessCoreExecutionRunObservationKind` | Stable descriptor rule family | Describes execution run/attempt facts; module owns AgentFramework execution and retry orchestration. |

## Finalization
| Public Surface Family | Owner Classification | Notes |
| --- | --- | --- |
| `ProcessFinalizerEvidenceDescriptorRules`, `ProcessFinalizerEvidenceDescriptor`, `ProcessFinalizerIntentEvidenceDescriptor`, `ProcessFinalizerResultEvidenceDescriptor`, `ProcessCoreFinalizerKind`, `ProcessCoreFinalizerBlockCauseKind` | Stable descriptor rule family | Describes finalizer intent/result facts; module owns finalizer invocation and transition application. |

## Diagnostics
| Public Surface Family | Owner Classification | Notes |
| --- | --- | --- |
| `ProcessRetryDiagnosticDescriptorRules`, `ProcessRetryDiagnosticDescriptor`, `ProcessNoProgressRetryDiagnosticDescriptor`, `ProcessProviderRepairDiagnosticDescriptor`, `ProcessRetryDiagnosticFailureKind` | Stable descriptor rule family | Describes retry/no-progress/provider repair facts; module owns provider health calls, repair, retry persistence, and recovery packets. |

## Routing
| Public Surface Family | Owner Classification | Notes |
| --- | --- | --- |
| `ProcessDispatchRoutePipeline`, `ProcessDispatchRoutePlanner`, `ProcessDispatchRouteEligibility`, `ProcessDispatchRouteSnapshot`, `ProcessDispatchRouteDecision`, `ProcessDispatchRouteStage`, `ProcessDispatchRouteKind`, `ProcessDispatchStepTransitionIntent`, `ProcessDispatchTransitionIntentRules` | Stable pure route rule family | Route selection, stage ordering, eligibility, and transition intent only; module owns claims and transition mutation. |

## Subprocess
| Public Surface Family | Owner Classification | Notes |
| --- | --- | --- |
| `ProcessSubprocessLifecycleRules`, `ProcessSubprocessRunFacts`, `ProcessSubprocessParentTransitionFacts` | Stable pure subprocess rule family | Parent transition facts and reason text only; module owns subprocess runtime and state mutation. |

## Denied Public API Families
- No `IProcessDriverPack`, `IProcessDriverRegistry`, `ProcessDriverRegistry`, driver pack, runtime selector, manager command, DI registration, storage service, EF context, AgentFramework execution client, file IO, workspace path resolver, or Blazor/UI type is part of the Core public surface.
