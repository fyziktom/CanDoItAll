# SB17 Proof Manifest

## Status

- Subbundle: `SB17`
- Status: `Completed`
- Owned requirements: `R09`, `R10`, `R11`
- Owned raw notes: generic memory workflow executor; operation settings; provider selection policy; compatibility mapping from old native executor ids; typed workflow result shaping; async accepted metadata; workflow executor registration.

## Semantic Invariant Contract

- Contract: `bundle://proof/SB17/semantic-invariants.md`

## Changed File Hashes

| File | After SHA-256 |
| --- | --- |
| `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs` | `c966a0e1d3998085cd1f3d97cfb469c4c0967893ca6c660d6607643bf1196667` |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/WorkflowExecutors/MemoryWorkflowExecutorContracts.cs` | `2beb6cf8554735b64fa9cdaeaf0ddf8667386ec447e21686519a657dbdc92750` |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/WorkflowExecutors/MemoryWorkflowExecutorDescriptorSource.cs` | `8ee186b3841244617917f08a5ad2bcdb0f5931ab4d8a6b0fd8d6e5015aca348f` |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/WorkflowExecutors/MemoryWorkflowExecutor.cs` | `9a38af941acd46286a2c6e2a9b3db64d213d9b73897dafe69dd8473781d03ce0` |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs` | `b9ca7f81821de800034b9e61b43fa2c063d9f9b529fde37ee97fba453586b36a` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/MemoryWorkflowExecutorTests.cs` | `f6644b5a5479c16376542c47308cb3577213c0e2a330856804b32dc4e339ba9a` |
| `bundle://proof/SB17/transcripts/failing-first-memory-workflow-executor-tests.txt` | `88440346513651bf822b7d0d1b32cdfd0050a4ca95502ede9f2ff603d7611acf` |
| `bundle://proof/SB17/transcripts/passing-memory-workflow-executor-tests.txt` | `7bccf71771fbfc5e6cb5190bfc8df025f0c5aecdbb448ebc6e04f8a4745ba9f0` |
| `bundle://proof/SB17/transcripts/source-audit-memory-workflow-executor-boundary.txt` | `84e30a8190ed371603b9c1f7da3633216342d6e61693903418c2184243794602` |
| `bundle://proof/SB17/transcripts/source-audit-memory-workflow-executor-dispatch-boundary.txt` | `5253f8f209dd8091fab6939dd493b6b1ae0a7ef85aa0ae68139925cf5f17b945` |
| `bundle://proof/SB17/transcripts/passing-solution-build.txt` | `3d5bd575611f2726d159bde8d5f941cce6d3fb289f2e29e732bc22f6823651bb` |
| `bundle://evidence/22-prepared-stage-validation-after-sb17.txt` | `e73182321d0dda1c9eba13fc7a971309c2620250430a99355b85dc72e7838ab5` |

## Command Transcripts

| Purpose | Transcript |
| --- | --- |
| Failing-first memory workflow executor tests before implementation | `bundle://proof/SB17/transcripts/failing-first-memory-workflow-executor-tests.txt` |
| Focused memory workflow executor tests | `bundle://proof/SB17/transcripts/passing-memory-workflow-executor-tests.txt` |
| Native dependency audit | `bundle://proof/SB17/transcripts/source-audit-memory-workflow-executor-boundary.txt` |
| Dispatch boundary audit | `bundle://proof/SB17/transcripts/source-audit-memory-workflow-executor-dispatch-boundary.txt` |
| Solution build | `bundle://proof/SB17/transcripts/passing-solution-build.txt` |
| Bundle prepared-stage validation after SB17 | `bundle://evidence/22-prepared-stage-validation-after-sb17.txt` |

## Passing Proof

- Failing-first transcript: exit code non-zero before implementation because `MemoryWorkflowExecutor`, `MemoryWorkflowExecutorSettings`, `MemoryWorkflowOperation`, `MemoryWorkflowExecutorDescriptorSource`, `MemoryWorkflowExecutorCompatibility`, and `WorkflowExecutorIds.Memory` did not exist.
- Focused workflow executor transcript: exit code `0`, 8 tests passed. It proves descriptor discovery, old native id compatibility mapping, context query result shaping, input query fallback, async accepted status shaping, typed no-provider behavior, capability denial before handler dispatch, manual source-scope denial before handler dispatch, and `AddAgentFrameworkModule` registration.
- Native dependency audit: exit code `0`, no native Cognitive Memory, Qdrant, or native memory implementation references found in the SB17 workflow executor surface. Lower-case legacy executor id strings are isolated compatibility data.
- Dispatch boundary audit: exit code `0`, executor calls `IMemoryOperationHandler` for all operations and has no direct memory provider registry or driver dispatch references.
- Solution build transcript: exit code `0`, with known NU1900 vulnerability-index fetch warnings and NU1903 `Microsoft.OpenApi` advisory warnings only.
- Bundle validation transcript: `bundle://evidence/22-prepared-stage-validation-after-sb17.txt`, exit code `0`.

## Source Assertions

- `WorkflowExecutorIds.Memory` defines the generic `memory.operation` executor id.
- `MemoryWorkflowExecutorDescriptorSource` exposes the generic memory executor through the existing workflow executor catalog source pattern.
- `MemoryWorkflowExecutorSettings` stores typed operation, provider, capability, source-scope, assignment, async, feedback, status, cancellation, event, and source snapshot settings.
- `MemoryWorkflowExecutor` maps workflow inputs into Memory Protocol v1 handler requests and returns the same typed memory result contracts as MAF runtime tools.
- Provider allowlists, capability allow/deny lists, and manual source-scope policy are enforced before handler dispatch.
- Context query, manual ingestion, feedback, status, cancellation, and event acknowledgement all delegate to `IMemoryOperationHandler`.
- `MemoryWorkflowExecutorCompatibility` maps old native workflow executor ids to `WorkflowExecutorIds.Memory` without registering duplicate legacy executor implementations.
- No shipped workflow template currently references old native memory executor ids; template-facing compatibility is provided by the new descriptor/default settings and isolated id mapping.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Generic memory executor id | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs` | descriptor and registration tests | stable executor id for workflow templates and migrated definitions | failing-first transcript failed before id existed |
| Workflow memory settings/contracts | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/WorkflowExecutors/MemoryWorkflowExecutorContracts.cs` | focused executor tests | typed settings cover query, ingestion, feedback, status, cancellation, event acknowledgement, provider policy, and compatibility mapping | capability/source denial tests assert typed policy failures |
| Workflow memory descriptor source | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/WorkflowExecutors/MemoryWorkflowExecutorDescriptorSource.cs` | descriptor source test | executor discoverable through existing catalog source pattern | test fails if descriptor id or implementation status changes |
| Workflow memory executor | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/WorkflowExecutors/MemoryWorkflowExecutor.cs` | query, input fallback, async accepted, no-provider, capability denial, and ingestion denial tests | requests are converted to Memory Protocol v1 handler requests and results are shaped back to workflow JSON | dispatch boundary audit and source-scope denial assert no bypass |
| DI registration | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs` | registration test | `IWorkflowExecutor` and `IWorkflowExecutorDescriptorSource` resolve through current AgentFramework module composition | test fails if registration is removed or lifetime changes |
| Boundary audits | `bundle://proof/SB17/transcripts/source-audit-memory-workflow-executor-boundary.txt` and `bundle://proof/SB17/transcripts/source-audit-memory-workflow-executor-dispatch-boundary.txt` | manifest source assertions | generic-only dependency boundary and handler-only dispatch remain observable | audit commands fail on native refs or direct driver/registry dispatch |

## Browser Validation

- Browser validation: `N/A`.
- Reason: SB17 changes workflow executor contracts, descriptor/source registration, service registration, and unit tests only. No browser-visible route or component behavior changed.

## Closure Decision

- SB17 closure gate: `Pass`.
- Reopened subbundles: `None`.
- Scope note: downstream context contributor/hard-link removal, UI surfaces, native extraction, host decoupling, data migration, test rebalance, e2e observability, and final cleanup remain owned by later subbundles. SB17 deliberately stops at generic workflow executor integration and isolated native-id compatibility mapping.
- Downstream permission: SB18 may start because generic workflow memory execution, provider selection policy, typed result shaping, denials, async accepted handling, registration, compatibility mapping, and dependency boundaries are proven.
