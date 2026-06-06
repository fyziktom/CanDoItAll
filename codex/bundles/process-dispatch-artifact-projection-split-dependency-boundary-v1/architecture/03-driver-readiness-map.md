# Documentation-Only Driver Readiness Map

This bundle prepares vocabulary for future drivers but must not implement production driver APIs.

| Future driver/evidence concept | Current module-local projection family | Production API now? |
| --- | --- | --- |
| `ExecutionArtifactEvidence` | Execution artifact projection coordinator | No |
| `ProcessMockEvidence` | Process mock artifact projection coordinator | No |
| `WorkspaceMutationEvidence` | Workspace-written artifact projection coordinator | No |
| `ExistingManagedArtifactEvidence` | Existing managed artifact projection coordinator | No |
| `ResponseTextEvidence` | Response-text artifact projection coordinator | No |
| `ProviderNativeBrowserEvidence` | Provider-native browser projection coordinator | No |
| `CompletedDecisionEvidence` | Completed-decision record-only coordinator | No |
| `ArtifactProjectionHost` | Internal dependency surface for projection families | Internal only |
| `ProjectionSourceCoordinator` | Internal module-local source-family coordinator | Internal only |

Future driver packs should depend on stable evidence vocabulary, not private dispatcher partial methods.
