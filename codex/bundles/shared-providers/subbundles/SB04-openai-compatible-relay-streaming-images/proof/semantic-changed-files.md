# SB04 semantic changed-file inventory

State: `PASS`, including the August 25 named wire-contract revalidation.

## Basis and classifications

This is the focused SB04 semantic inventory, not the cumulative `git diff` from repository HEAD.
The shared worktree contains uncommitted SB00-SB04 work while branch `providers-shared` remains at
`e46f81d5ee33627dccb548732725e1c37e980ab5`. The SB03 closing hashes are the phase boundary.

- `A04`: file introduced during SB04.
- `M04`: file existed at the SB03 boundary and SB04 changed its bytes/semantics.

No `CARRY` rows are used. Unchanged SB00-SB03 dependencies are intentionally omitted instead of
being asserted byte-identical without need. The original tables preserve the SHA-256 values at the
first SB04 closure. Later subbundles legitimately changed some of those files, so the August 25
overlay below records the current bytes that triggered and proved this narrow reopen. This document
cannot contain its own stable hash; the proof-wide integrity record is generated afterward in
`proof/hashes.sha256`.

## Neutral relay and Http implementation

| Class | Repository path | SB04 closure SHA-256 |
| --- | --- | --- |
| `A04` | `src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderRelayRuntimeContracts.cs` | `b0c4a4f47864add2c675bd33e12c54d75a39833b86eed6f5cc28c6350d3c2c02` |
| `M04` | `src/Integration/CanDoItAll.SharedProviders.Http/CanDoItAll.SharedProviders.Http.csproj` | `87d68bbadb7803286c94f89340ce7e9f536941145aa97fc1945dfef2fbb55e81` |
| `A04` | `src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderHttpRelayClient.cs` | `dc7e93ee0580fcc7e560b6ccdbf9741f1e3fa92d5a9d5b55bc795293c0c74d73` |
| `M04` | `src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderHttpServiceCollectionExtensions.cs` | `d1867b42b94a8907877868f013597408bfe0d6d3df2f4feb9a1b847096bc7963` |
| `A04` | `src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelayAdapterRegistry.cs` | `3a6ddb19afc2b822218c90a80d2ef4d1ed879871ec7f1bde97c110b23a8a26e7` |
| `A04` | `src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelayPolicies.cs` | `34f3ab55250f973bc95785912bc47dd54ff2f088b4daaa70c9dc63f5daad1bbb` |
| `A04` | `src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelayRequestPolicy.cs` | `3959d31361604656b3ce21557c75016b2b86f1d51e6d0ef617d37e5007fff5fd` |
| `M04` | `src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelaySupportCatalog.cs` | `93b5d68a6e6af73c59d40af519dbab0d7961d1ac309405e8149707b30f42f226` |
| `A04` | `src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderSseRelayStream.cs` | `e67c00d7f5406ef3b2473344e3dc4f325ed689c85d44c4b8032bb5317d914601` |

These files own the neutral runtime values/ports, exact per-surface policy, five-row typed adapter
registry, connector-owned URI/header/failure/usage behavior, bounded buffered/SSE client, distinct
timeouts, and typed streaming completion. The final request-policy hash includes surface-specific
tools/choice/schema/image shapes and the absent-versus-malformed `n` repair.

## Workspace audit, image ownership, recovery, and persistence

| Class | Repository path | SB04 closure SHA-256 |
| --- | --- | --- |
| `A04` | `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderAuditedRelayStream.cs` | `8cb4df47b5071622abfe15985dca577f2f60dd2cab4be887a2259bf79f7b7f64` |
| `A04` | `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderImageExecutionTargetResolver.cs` | `ce8b13a7a148886ac954c7b0fe28ad25b274a232025be177ff97bc3d785912a2` |
| `M04` | `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderInvocationAuditService.cs` | `ce919c90ff9f58fc4179751517d62bb93a4dfaf865adaa89f125012d35560062` |
| `M04` | `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderInvocationRecord.cs` | `058cb8eeadde866bdc3273b3224a841b161b807d22c99f10e7240a073224d684` |
| `M04` | `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderInvocationRecordConfiguration.cs` | `ef3859e7fbdd3a612ddf8e42bdd5ac31aeb91aa61126026cf2192d8515ea727b` |
| `A04` | `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderInvocationRecoveryService.cs` | `7c1a5d11b7180ba69e32f290bcb5726a4c4ca5ca7e10fa78e308edba7f64c1d2` |
| `M04` | `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderInvocationTransitions.cs` | `e6545c145440442523798c75f65b096f327f7fc0ee530c9c04bfda7ddbd2f05a` |
| `A04` | `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderRelayApplicationService.cs` | `a8e28d4849ff54bd40f6b3d75ee08b27a8856ff6f725b4fc5bf0c0a92a5e3109` |
| `M04` | `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderStates.cs` | `c5bb2c36f558dda17304fa9f2094c861711a4a54ae4d9592ec93aa01fa6817ef` |
| `M04` | `src/Modules/CanDoItAll.Modules.Workspace/Services/WorkspaceModuleServiceCollectionExtensions.cs` | `1ab47dc0b93a9099e2afbca41f3c7dbe5bc2470ae2e3d9c4018a9d32f7adc7a1` |
| `A04` | `src/Modules/CanDoItAll.Modules.Workspace/Properties/AssemblyInfo.cs` | `ae2d32c57e5017f0731e80d2cfbf7721d2ec6e7207d4c6dd7605a92985790d07` |
| `M04` | `src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260824224847_AddSharedProviderPersistence.cs` | `cbd1e533d11c535c49e4fd4e9956346a976d1643f822f42d36b4b6cb318a1f18` |
| `M04` | `src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260824224847_AddSharedProviderPersistence.Designer.cs` | `282bca98a6fd88251959fc920cfd9fc144f3f13b0092b1f39c2277da1f67a638` |
| `M04` | `src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs` | `c49fe1a976dec36ee29c53142803362e932eccfc01d71952acab8766b3a8d275` |

These files own current-state route/secret resolution, metadata-only begin/finalize, operation-aware
image usage, Workspace-owned image execution resolution, bounded stale-row recovery, and DI. The
migration was originally introduced by SB02; SB04 materially changed its invocation columns and
usage constraint, so it is correctly `M04`, not carry-over. The EF configuration, migration,
designer, and snapshot now contain the aligned Chat/Responses-token versus Images-count matrix.
`AssemblyInfo.cs` grants only the integration test assembly access to the internal immutable
recovery schedule; it does not create a public timing option.

## Existing usage direction and AgentFramework bridges

| Class | Repository path | SB04 closure SHA-256 |
| --- | --- | --- |
| `M04` | `src/MAF/Common/CanDoItAll.AgentFramework.Usage/ProviderUsageContracts.cs` | `b8707f5175a787bb5a8c24968f44bc358d4a286e75694f239a55715e66297d8f` |
| `M04` | `src/MAF/Common/CanDoItAll.AgentFramework.Usage/ProviderUsageQueryService.cs` | `128144b951c98bbdb624a36a5de90823df3201f5b83c38a0e263f85d08cd989f` |
| `M04` | `src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj` | `4f8ba36fa2813d243e650ebbb4c041ae5f8017ec9eba2fab4903058a125c485d` |
| `A04` | `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedProviderImageCapabilityRelay.cs` | `1f1f610d914634b37f502f4cbd885e9bf00771d1d06ae608c347ce3199d89e88` |
| `A04` | `src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedProviderRelayUsageProjectionSource.cs` | `7323b0b0e0eb05f3b1ccf6b363eda1d9e9e0935fc9f15004f3ab700056a28e93` |
| `M04` | `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs` | `f9b583cb58f7fe7aa30b7fab9774f13ec5e9a57d9067e5cbb27e409a1baac9d6` |

These changes reuse the existing provider-usage direction, add ABI-preserving image-count
properties/checked aggregation, explicitly fail inconsistent stored usage, and keep the image
capability bridge outside neutral layers. No second cost or usage ledger was introduced.

## Web surface

| Class | Repository path | SB04 closure SHA-256 |
| --- | --- | --- |
| `M04` | `src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs` | `bc0e73c4a5bde129aedf898678bd136da05aee8511041a7ae8e5ea4c198ebe86` |
| `M04` | `src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs` | `b56b36f8ad53f873fa03789c405ef915d6fc9c6b0c0f1fd79746718cae3b8ba8` |
| `M04` | `src/App/CanDoItAll.Web/Api/SharedProviderApiResponseWriter.cs` | `c01d62fd7687b89ef9d9e2ae395918e038a6e68820a2e9e63c92aeb6e442913a` |
| `A04` | `src/App/CanDoItAll.Web/Api/SharedProviderInferenceApi.cs` | `81f9b50c73e737f76f67b2b62e7ee66b90a7ce1231142a13477f7b8d1bcbbb78` |
| `A04` | `src/App/CanDoItAll.Web/Api/SharedProviderInferenceOpenApiContract.cs` | `1ff4fadc2941b977229bbd2742106e2e50a753960fb35f11379f11fddf04fb45` |
| `A04` | `src/App/CanDoItAll.Web/Api/SharedProviderOpenAiServerSentEventWriter.cs` | `527aa0d990a1145de7e8541f2e7950076113fc0e8cc0bc3e1971828ff400103c` |

These files expose exactly three invoke-authorized POST surfaces, bounded request reads,
OpenAI-compatible sanitized errors, base64-only image output, incremental SSE, and truthful
marker-scoped OpenAPI. They do not add audio, wildcard proxying, or inference ETag semantics.

## Focused tests changed by SB04 or its semantic reopen

| Class | Repository path | SB04 closure SHA-256 |
| --- | --- | --- |
| `M04` | `tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` | `891fc425175551eab7b1aa58864c1d521562f8a7ce5f43b618aaae545755e488` |
| `A04` | `tests/Unit/CanDoItAll.Tests.Unit/SharedProviderRelayPolicyTests.cs` | `8947f4c9df96ae2d81f303a5f9410287adf13a06abaa07e22a9b92e14ea57189` |
| `M04` | `tests/Unit/CanDoItAll.Tests.Unit/ProviderUsageAggregationTests.cs` | `bc9d90c221488b21c0145daa6271935a2d188a13102fd8b16403f4ea7d9fa270` |
| `M04` | `tests/Unit/CanDoItAll.Tests.Unit/SharedProviderStateModelTests.cs` | `cd4242b81b35f9d7fc708e618fbb2b7d0bfdc1e09a5c5647e9658c7eb6a8b1ab` |
| `A04` | `tests/Integration/CanDoItAll.Tests.Integration/SharedProviderOpenAiCompatibilityIntegrationTests.cs` | `7298c9afe7ad4e43b6d36ef506c73f66d0d9f6c20cea693fb33761be628bd167` |
| `A04` | `tests/Integration/CanDoItAll.Tests.Integration/SharedProviderStreamingIntegrationTests.cs` | `7524ba01b7d082cbcdb61fc5d08cc2ac637335013bf4a55afca41e54e117f40c` |
| `M04` | `tests/Integration/CanDoItAll.Tests.Integration/SharedProviderPersistenceIntegrationTests.cs` | `ecb0f72bb8865e516234cbfa25799082a51ea59a30b1824a9c8abff5c3eb6ff3` |

The relay policy class remains exactly 24 Facts, compatibility exactly 22, and streaming exactly
12. Usage aggregation remains its existing 7-Fact class and is additive supporting proof. The
SB02-owned state/persistence classes retain their governed 18/14 counts after SB04 invalidation and
revalidation. Existing `ApiTestHost.cs` and the integration test project are omitted: their final
hashes equal the SB03 boundary and SB04's PostgreSQL fixture changes are contained in the new
compatibility test file.

## August 25 named-invalidation overlay

These are the current bytes relevant to the reopened allowlist/capability/streaming contract. The
SB07 checkpoint class remains SB07-owned supporting proof; the deterministic upstream files are
test-only and do not create a production bypass or dependency.

| Repository path | Current SHA-256 |
| --- | --- |
| `src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelayRequestPolicy.cs` | `d870c969f740208f91aa8111bd17f2c6ee345a846594d60a73d0cc4fdf871e36` |
| `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderRelayApplicationService.cs` | `0c58ed11edfa49a7b96e2f42b145e412a142e3c5b3ab58ad211cd5511d28a3df` |
| `src/Modules/CanDoItAll.Modules.Workspace/Models/WorkspaceModels.cs` | `ce0b3e60abd0fb13aa34ab7d9d7acd048c294fef85f7936da77b402683c27c0e` |
| `tests/Unit/CanDoItAll.Tests.Unit/SharedProviderRelayPolicyTests.cs` | `75147341f683a7411c5ff05a733303e8d34dc37925f427d9d6925a04deec5969` |
| `tests/Integration/CanDoItAll.Tests.Integration/SharedProviderOpenAiCompatibilityIntegrationTests.cs` | `799e8676d1c6345f65398b2dc9ddc94d008a0d84a67fc38e9a28e17a2d82dbf1` |
| `tests/Integration/CanDoItAll.Tests.Integration/SharedProviderBackendCheckpointIntegrationTests.cs` | `56e646fae089063c5e8e3f49c67de60ab1a703305dab95dd42a5f934a189568a` |
| `tests/Support/CanDoItAll.SharedProviders.TestUpstream/TestControl.cs` | `563f36275afb9568209b624773bd9795116d8d7a0055aa45a6348f7e47cf2bdf` |
| `tests/Support/CanDoItAll.SharedProviders.TestUpstream/OpenAiFixtureEndpoints.cs` | `b0fe15a708e4b9ca654c3fb9f601de0260ab560118b4ae6efe7cdd47d9f23fb6` |

The authoritative revalidation discovery/run artifacts are the fresh Release 24/22/12 transcripts
named in `proof-manifest.json`. The two compile failures and the first overbroad security scan are
retained as honest chronology, not passing evidence.

## Governance/proof additions in this bounded task

SB04 also creates/updates its README, handoff, test selection, proof manifests, exact transcripts,
architecture/reference artifacts, behavior/security records, closure-validator evidence, and
hash manifest. This semantic inventory enumerates production and focused test bytes; the generated
`proof/changed-files.md` and `proof/hashes.sha256` are the authoritative complete proof-file and
integrity inventories after all narrative/status edits finish.
