# Documentation-Only Driver Readiness Map

No production driver API may be created in this bundle.

Future driver/evidence concepts that this bundle prepares semantically:

| Future concept | Current runtime meaning | Do now? |
| --- | --- | --- |
| `ProjectionSourceEvidence` | Which source family produced a process artifact record. | Document only |
| `ExecutionArtifactEvidence` | Agent execution emitted a durable file artifact. | Document only |
| `WorkspaceMutationArtifactEvidence` | Workspace write/stat/read evidence produced a projectable artifact. | Document only |
| `ManagedArtifactReferenceEvidence` | Existing managed artifact file satisfied a required expectation. | Document only |
| `ResponseNarrativeEvidence` | Structured or plain response text was projected as a governed artifact. | Document only |
| `BrowserNativeEvidence` | Provider-native browser output file satisfied an evidence expectation. | Document only |
| `DecisionRecordEvidence` | Completion decision was recorded as a process decision artifact. | Document only |

The future driver layer should consume stable source-family vocabulary. It must not be introduced here.
