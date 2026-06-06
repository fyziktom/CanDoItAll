# Documentation-Only Driver Readiness Map

This bundle prepares vocabulary for future process drivers but does not create production driver APIs.

| Future driver/evidence concept | Current projection meaning | Action now |
| --- | --- | --- |
| `ArtifactProjectionEvidence` | Durable evidence that a projection source produced or recorded a process artifact. | Document only. |
| `WorkspaceFileEvidence` | Workspace-written or existing managed file projected as artifact. | Express as module-local projection model only. |
| `BrowserOutputEvidence` | Provider-native browser output projected as artifact. | Express as module-local projection model only. |
| `DecisionRecordEvidence` | Record-only completed decision artifact. | Express as module-local projection model only. |
| `DriverProducedArtifactCandidate` | A future driver could propose artifact candidates. | Do not implement now. |
| `IProcessDriverPack` | Future driver-pack interface. | Explicitly forbidden in this bundle. |
