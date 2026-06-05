# Documentation-Only Driver Readiness Map

No production driver API is allowed.

Potential future driver/evidence concepts prepared by this bundle:

| Future concept | Current process concept | Current owner |
| --- | --- | --- |
| `ArtifactMaterializationIntent` | missing upstream artifact target + rerun directive | `ProcessDispatchMissingUpstreamArtifactMaterializationPlan` |
| `EvidenceGap` | missing artifact input list | `ProcessMissingUpstreamArtifactMaterializationFacts` |
| `DatabaseRuntimeRequirement` | database profile requirement failure | `ProcessDispatchDatabaseRequirementDecision` |
| `DriverCanRecoverMissingArtifact` | runnable upstream source step | `ProcessMissingUpstreamArtifactMaterializationFactsResolver` |
| `DriverRecoveryDirective` | rerun operator reason | `ProcessMissingUpstreamArtifactRerunRequestBuilder` |

Notes:

- Future drivers may eventually propose or satisfy materialization intents.
- This bundle only stabilizes local vocabulary and proof.
- Do not add driver interfaces or registry.
