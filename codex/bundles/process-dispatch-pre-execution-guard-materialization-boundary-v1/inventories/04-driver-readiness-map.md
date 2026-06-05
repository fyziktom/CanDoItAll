# Documentation-Only Driver Readiness Map

No production driver API is allowed.

Potential future driver/evidence concepts prepared by this bundle:

| Future concept | Current process concept | Current owner |
| --- | --- | --- |
| `ArtifactMaterializationIntent` | missing upstream artifact target + rerun directive | upstream materialization planner |
| `EvidenceGap` | missing artifact input list | upstream gap facts |
| `DatabaseRuntimeRequirement` | database profile requirement failure | database blocker |
| `DriverCanRecoverMissingArtifact` | runnable upstream source step | materialization target selection |
| `DriverRecoveryDirective` | rerun operator reason | rerun request builder |

Notes:

- Future drivers may eventually propose or satisfy materialization intents.
- This bundle only stabilizes local vocabulary and proof.
- Do not add driver interfaces or registry.
