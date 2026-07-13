# SB16 Proof Manifest

## Status

- Subbundle: `SB16`
- Status: `Completed`
- Owned requirements: `R09`, `R10`, `R11`
- Owned raw notes: generic MAF memory runtime tools; agent-level provider selection policy; capability/source-scope denials; typed tool result shaping; async accepted metadata; runtime tool provider registration.

## Semantic Invariant Contract

- Contract: `bundle://proof/SB16/semantic-invariants.md`

## Changed File Hashes

| File | After SHA-256 |
| --- | --- |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/AgentMemoryAccessMetadata.cs` | `a203d26fd0aac1ddb2ae4eba5e93d61a0db8262038dcae3623ec831e36a88f32` |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/MemoryAgentRuntimeToolContracts.cs` | `10a859ccf35c58c2c898fc23d0e58e9f7421ad0b6d2e16aa5cedf8247c7f8b12` |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/MemoryAgentRuntimeToolProvider.cs` | `9e066c61bcea5148712afa1bc8d0f5ba6fda7f0944900a2a3d92fd30aa2a5508` |
| `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs` | `fa328b42e26febd34aff324e948ad24cacf01c46be2bc8ba13d2883d51b478aa` |
| `repo://tests/Unit/CanDoItAll.Tests.Unit/MemoryAgentRuntimeToolProviderTests.cs` | `2d1e784d665cc578a5cce142253e80dbc6db8ca2b1d423caee864139a1e833e6` |
| `bundle://proof/SB16/transcripts/failing-first-memory-tool-provider-tests.txt` | `e4b399d23841574cd69a262f9b8485884bc7caa250dfd9207bfd501fb715a861` |
| `bundle://proof/SB16/transcripts/passing-memory-agent-runtime-tool-provider-tests.txt` | `3a2d6bc4f91b2076bf1c2531eff8c7ce318d25c6637fecfd0b56487859b4e655` |
| `bundle://proof/SB16/transcripts/source-audit-memory-tool-provider-boundary.txt` | `c318792254a97e926407416975b178159e5747ae96074040bdb91ae09e8903ee` |
| `bundle://proof/SB16/transcripts/source-audit-memory-tool-provider-dispatch-boundary.txt` | `432b00830ecdae51bb5e7b3e7eafc3ec539621c6c195214af763d6b53a1b0aa3` |
| `bundle://proof/SB16/transcripts/passing-solution-build.txt` | `2c59a5feb2584f8b1e23c6f1a3958b7329fef0ac089e268ee0e9d77936474731` |
| `bundle://evidence/21-prepared-stage-validation-after-sb16.txt` | `e73182321d0dda1c9eba13fc7a971309c2620250430a99355b85dc72e7838ab5` |

## Command Transcripts

| Purpose | Transcript |
| --- | --- |
| Failing-first MAF memory tool provider tests before implementation | `bundle://proof/SB16/transcripts/failing-first-memory-tool-provider-tests.txt` |
| Focused MAF memory runtime tool provider tests | `bundle://proof/SB16/transcripts/passing-memory-agent-runtime-tool-provider-tests.txt` |
| Native dependency audit | `bundle://proof/SB16/transcripts/source-audit-memory-tool-provider-boundary.txt` |
| Dispatch boundary audit | `bundle://proof/SB16/transcripts/source-audit-memory-tool-provider-dispatch-boundary.txt` |
| Solution build | `bundle://proof/SB16/transcripts/passing-solution-build.txt` |
| Bundle prepared-stage validation after SB16 | `bundle://evidence/21-prepared-stage-validation-after-sb16.txt` |

## Passing Proof

- Failing-first transcript: exit code non-zero before implementation because `MemoryAgentRuntimeToolProvider` and `AgentMemoryAccessSettings` did not exist.
- Focused tool provider transcript: exit code `0`, 8 tests passed. It proves tool exposure and metadata, context result shaping, two agents selecting different memory providers, typed no-provider and unsupported-capability results, async accepted status shaping, manual source-scope denial before dispatch, agent memory metadata round-trip, and `AddAgentFrameworkModule` registration.
- Native dependency audit: exit code `0`, no native Cognitive Memory, Qdrant, or `native.cognitiveMemory` references found in the SB16 tool provider surface.
- Dispatch boundary audit: exit code `0`, provider calls `IMemoryOperationHandler` for all operations and has no direct memory provider registry or driver dispatch references.
- Solution build transcript: exit code `0`, with known NU1900 vulnerability-index fetch warnings and NU1903 `Microsoft.OpenApi` advisory warnings only.
- Bundle validation transcript: `bundle://evidence/21-prepared-stage-validation-after-sb16.txt`, exit code `0`.

## Source Assertions

- `MemoryAgentRuntimeToolProvider` implements the existing `IAgentRuntimeToolProvider` pattern and is registered through `AddAgentFrameworkModule`.
- `AgentMemoryAccessMetadata` stores typed provider/capability/source policy settings under the agent `memory` configuration root.
- `MemoryAgentRuntimeToolProvider` builds `MemoryProviderSelectionPolicy` from explicit tool input, preferred/default provider settings, and agent/workflow/process assignments; it does not dispatch to hidden defaults.
- Provider allowlists, capability allow/deny lists, and manual source-scope policy are enforced before handler dispatch.
- Context query, manual ingestion, feedback, status, cancellation, and event acknowledgement all delegate to `IMemoryOperationHandler`.
- Tool outputs shape context summary, sections, citations, warnings, confidence, feedback handle, operation id, dispatch state, and async accepted status for agent consumption.
- The new MAF tool surface references only generic memory abstractions/application contracts, not native Cognitive Memory implementation types.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Agent memory access metadata | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/AgentMemoryAccessMetadata.cs` | metadata round-trip test | normalized provider, capability, source-scope, and assignment settings feed policy creation | default/disabled settings remove the memory root |
| Runtime tool contracts | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/MemoryAgentRuntimeToolContracts.cs` | focused AITool invocation tests | string enum statuses and typed result records round-trip through tool JSON | failing-first transcript failed before contracts existed |
| Runtime tool provider | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/MemoryAgentRuntimeToolProvider.cs` | context query, ingestion, no-provider, unsupported capability, async accepted, and two-agent tests | requests are converted to Memory Protocol v1 handler requests and results are shaped back to MAF | source-scope denial asserts no handler dispatch |
| DI registration | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs` | registration test | `IAgentRuntimeToolProvider` resolves through current MAF provider composition path | test fails if registration is removed or lifetime changes |
| Boundary audits | `bundle://proof/SB16/transcripts/source-audit-memory-tool-provider-boundary.txt` and `bundle://proof/SB16/transcripts/source-audit-memory-tool-provider-dispatch-boundary.txt` | manifest source assertions | generic-only dependency boundary and handler-only dispatch remain observable | audit commands fail on native refs or direct driver/registry dispatch |

## Browser Validation

- Browser validation: `N/A`.
- Reason: SB16 changes runtime tool provider contracts, service registration, and unit tests only. No browser-visible route or component behavior changed.

## Closure Decision

- SB16 closure gate: `Pass`.
- Reopened subbundles: `None`.
- Scope note: downstream contributors/executors for deferred retrieval, recall, embedding, projection, association, GraphRAG, telemetry, triggers, orchestrator, and admin UI remain owned by later subbundles. SB16 deliberately stops at the generic MAF runtime tool provider surface.
- Downstream permission: SB17 may start because generic MAF memory tools, provider selection policy, typed result shaping, denials, async accepted handling, registration, and dependency boundaries are proven.
