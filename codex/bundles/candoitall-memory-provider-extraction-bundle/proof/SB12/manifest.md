# SB12 Proof Manifest

## Status

- Subbundle: `SB12`
- Status: `Completed`
- Owned requirements: `R07`, `R06`
- Owned raw notes: process, workflow, artifact, agent-session, and process-completion source gateway adapters; same source snapshot contract family; denied process scope; workflow runtime compatibility; unavailable process provider diagnostics; provider-driver boundary.

## Semantic Invariant Contract

- Contract: `bundle://proof/SB12/semantic-invariants.md`

## Changed File Hashes

| File | After SHA-256 |
| --- | --- |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs` | `7fbe4851fe10fefb48cfe2b2785a86f804740c783f5717d52eb1f2b7fdf66f71` |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj` | `9891bbcab900405edf9f1954ce2477d5f1d62be5f038599853e3ab1b9b3c3e93` |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/WorkflowRuntimeMemorySourceGatewayAdapter.cs` | `2cd5d0bedd4776883bed2adfc62aeaabeff5d868a8a69665d4e284141a10e49c` |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs` | `a88cb37b7de8b2cb55e4607a05a2e88aa4da25b4158c8ef267b8d00bf78ed9ed` |
| `repo://src/Modules/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj` | `4cf2274d485ae59e3bbaa9e4f2b266450fea7377dc8bae37ce0c20828d0cdd6d` |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | `d54eb87e73eb8a3a96518faf47cd05e38a40c92b508abae3ddbd510101effdb3` |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs` | `142ed9e16f3c8031853acd7e2f87d08bec9d21c4408789c08d68f1bb41908a04` |
| `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeMemorySourceGatewayAdapter.cs` | `f50eb28bb222e6797ee25a1fc0f0e41856584c7f1f6891468e8463509c0fa38a` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeSourceGatewayAdapterTests.cs` | `e159e25938dccadc0b8595d911e4a7e6a6862d2ef58d5c010060a6608867abc9` |
| `bundle://proof/SB12/transcripts/passing-memory-test-suite.txt` | `367a773f60d4d2ef5d67cd20575056c043a0ad94b96844826682811f3a114794` |
| `bundle://proof/SB12/transcripts/passing-runtime-source-unit-tests.txt` | `a08b3a6d1662476ce0230ad2dffbd0e0a300480a48e1346d6da594675ce5cfdd` |
| `bundle://proof/SB12/transcripts/passing-solution-build.txt` | `ee45c4b3e788f9b9c4bfbb7a703dd25d42bf8f1e5b1263c334cc3c7853dfcb3b` |
| `bundle://proof/SB12/transcripts/passing-source-gateway-tests.txt` | `be523f3861f4b9b184f70976b7c97b294931850fa52c456fa16555751a3650aa` |
| `bundle://proof/SB12/transcripts/passing-workbench-source-integration-tests.txt` | `9bccd0c4e748e0d85358208ad7d6920c87447e4bfca93a2d90d22c3544b52370` |
| `bundle://proof/SB12/transcripts/source-audit-adapter-registration.txt` | `eaa35bdccd06520fb9145235595bfe4d5b0f4d8f612c2a9175827f084a19588c` |
| `bundle://proof/SB12/transcripts/source-audit-anti-stub.txt` | `3e9cc2e96dedc1ac9df7c6d040a89afeb56f00ebff41d98b26f9f2909a877a5b` |
| `bundle://proof/SB12/transcripts/source-audit-provider-driver-boundary.txt` | `834f05a7189801e5de0dc03dc1af2dd9519b99e8121eb66955dccf23810c47cc` |
| `bundle://proof/SB12/transcripts/source-audit-source-snapshot-contract-family.txt` | `20e9844cc2d308190d8c240a47865b1dda017366a161c42976907dd6ad5b0800` |
| `bundle://evidence/17-prepared-stage-validation-after-sb12.txt` | `e73182321d0dda1c9eba13fc7a971309c2620250430a99355b85dc72e7838ab5` |

## Command Transcripts

| Purpose | Transcript |
| --- | --- |
| Focused process/workflow source adapter unit tests | `bundle://proof/SB12/transcripts/passing-runtime-source-unit-tests.txt` |
| Focused generic source gateway regression tests | `bundle://proof/SB12/transcripts/passing-source-gateway-tests.txt` |
| Workbench source integration regression tests | `bundle://proof/SB12/transcripts/passing-workbench-source-integration-tests.txt` |
| Full generic memory test suite | `bundle://proof/SB12/transcripts/passing-memory-test-suite.txt` |
| Solution build | `bundle://proof/SB12/transcripts/passing-solution-build.txt` |
| Provider driver boundary audit | `bundle://proof/SB12/transcripts/source-audit-provider-driver-boundary.txt` |
| Adapter registration audit | `bundle://proof/SB12/transcripts/source-audit-adapter-registration.txt` |
| Source snapshot contract family audit | `bundle://proof/SB12/transcripts/source-audit-source-snapshot-contract-family.txt` |
| Anti-stub audit | `bundle://proof/SB12/transcripts/source-audit-anti-stub.txt` |
| Bundle prepared-stage validation after SB12 | `bundle://evidence/17-prepared-stage-validation-after-sb12.txt` |

## Passing Proof

- Runtime source unit transcript: exit code `0`, four `ProcessRuntimeSourceGatewayAdapterTests` passed, including process runtime snapshot content, denied process scope, workflow request translation, and module registration.
- Generic source gateway transcript: exit code `0`, seven `MemorySourceGatewayTests` passed after adding process/workflow adapters.
- Workbench integration transcript: exit code `0`, four `WorkbenchSourceSnapshotIntegrationTests` passed to prove SB12 did not regress the SB11 source gateway path.
- Full memory transcript: exit code `0`, all 61 generic memory tests passed.
- Solution build transcript: exit code `0`, with known NU1900 vulnerability-index fetch warnings and NU1903 `Microsoft.OpenApi` advisory warnings only.
- Bundle validation transcript: `bundle://evidence/17-prepared-stage-validation-after-sb12.txt`, exit code `0`.

## Source Assertions

- Process runtime adapter and provider live in `CanDoItAll.Modules.Processes`; generic HTTP/MCP drivers contain no process, workflow, or adapter references.
- Workflow runtime adapter lives in `CanDoItAll.Modules.AgentFramework` and wraps the existing `IWorkflowRuntimeEvidenceSourceProvider` instead of forking workflow snapshot logic.
- Processes module registers the real `ProcessRuntimeEvidenceSourceProvider` and process source gateway adapter; AgentFramework preserves `UnavailableProcessRuntimeEvidenceSourceProvider` diagnostics for hosts without Processes and registers the workflow adapter.
- Source snapshot contract family audit proves `MemorySourceSnapshot`, `ProcessRuntimeEvidenceSourceRequest`, and `WorkflowRuntimeEvidenceSourceRequest` remain canonical in MAF Core.
- Anti-stub audit found no SB12-relevant `TODO`, `NotImplementedException`, placeholder, fixture-only, or fake-only markers in the new adapter/provider surface.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Process runtime source provider | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs` | `Process_runtime_source_provider_exposes_run_step_agent_artifact_and_completion_context` in `bundle://proof/SB12/transcripts/passing-runtime-source-unit-tests.txt` | reads process persistence and execution observations into MAF `MemorySourceSnapshot` pages | denied process scope test proves provider dispatch is blocked before source access |
| Process runtime gateway adapter | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeMemorySourceGatewayAdapter.cs` | generic source gateway and process denied-scope unit tests | maps generic process source requests into `ProcessRuntimeEvidenceSourceRequest` | source/scope mismatch is rejected by gateway policy and adapter checks |
| Workflow runtime gateway adapter | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/WorkflowRuntimeMemorySourceGatewayAdapter.cs` | workflow adapter translation unit test | wraps existing `IWorkflowRuntimeEvidenceSourceProvider` with generic source gateway descriptor | requested source/scope mismatch throws explicit adapter errors |
| Process completion feedback hook metadata | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs` | process runtime source unit test asserts `feedbackHook=process-runtime-completion` | completion snapshots summarize terminal runtime state for later delayed feedback | snapshot exposes counts and hook identity only, not mandatory feedback submission |
| Artifact reference snapshot | `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRuntimeEvidenceSourceProvider.cs` | process runtime source unit test asserts `artifact-id` storage reference and content hash | process artifact ledger rows become references without copying artifact payload bytes | test asserts reference metadata and no payload-byte access path |
| Fallback unavailable process diagnostics | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs` | adapter registration audit and unit DI test | AgentFramework keeps fallback provider when Processes module is absent | Processes module registers the real provider, avoiding fallback shadowing in composed hosts |

## Browser Validation

- Browser validation: `N/A`.
- Reason: SB12 adds non-UI source gateway adapters and runtime source providers. No browser-visible route or component behavior was changed.

## Closure Decision

- SB12 closure gate: `Pass`.
- Reopened subbundles: `None`.
- Host validation: solution build passed after the process and workflow adapter registrations were added.
- Downstream permission: SB13 CRM/resource/manual source adapters may start after bundle-level validation passes.
