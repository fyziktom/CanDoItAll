# Driver Readiness Candidate Map

Documentation-only. No production driver API in this bundle.

| Candidate fact | Future driver relevance | Current owner after this bundle |
| --- | --- | --- |
| Project id / project-structure context | Determines which future helper may inspect project context. | `ProcessDispatchCandidateHydrationSnapshot` / `ProcessDispatchTechnicalAgentBindingCoordinator`. |
| Step kind and route kind | Distinguishes subprocess, workflow, direct-agent, manager recovery. | Candidate route snapshot / assembler. |
| Expected artifacts | Defines evidence contract that future helpers may satisfy. | Existing `LoadExpectedArtifactsAsync` plus `ProcessDispatchCandidateHydrationSnapshot` source context. |
| Artifact inputs | Defines upstream artifact dependencies and read-only input material. | `ProcessDispatchArtifactInputAssembler`. |
| Technical agent id and binding status | Determines executor availability and tool profile. | `ProcessDispatchTechnicalAgentBindingCoordinator`. |
| Required browser/build/test evidence | Later maps to domain helpers. | Existing validation/tool rules; documentation only here. |
| Manual recovery directive | Later may guide recovery helper behavior. | `ProcessDispatchRecoveryQueryHelper`. |

Do not implement any driver from this map in this bundle.
