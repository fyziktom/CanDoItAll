# Sharing, pricing, and caller attribution analysis

Prepared 2026-08-28. Analysis only: no application code, database, credential, catalog synchronization, or live provider invocation was changed. CodeAnalytics snapshot `snap-20260828134930-4eb1620a` was reused for exact ProviderManagement owner lookup; the parent analysis owns its dashboard/dependency proof. HTTP, Web, MAF and test files outside that narrow snapshot were read directly. No deployed row or live price was inspected by this analysis.

## 1. Confirmed behavior and cause candidates

| Finding | Evidence | Consequence |
|---|---|---|
| All normal relay finalizations explicitly write `Price: null` and `PricingCompleteness.Unavailable`. | [SharedProviderAuditedRelayStream.cs:64](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderAuditedRelayStream.cs:64) | Confirmed missing publisher-side execution pricing, not merely a display issue. |
| Buffered responses and streaming responses use the same finalizer. | [SharedProviderRelayApplicationService.cs:180](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRelayApplicationService.cs:180), [buffered completion:339](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRelayApplicationService.cs:339) | Fix must cover both transports and preserve stream completion semantics. |
| Relay usage projection returns `Unpriced` when the persisted price is missing/unavailable. | [SharedProviderRelayUsageProjectionSource.cs:221](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedProviderRelayUsageProjectionSource.cs:221) | Direct explanation for publisher relay rows displaying unpriced. The particular localhost row is not proven to be this workload. |
| Published model prices already come from persisted provider pricing and are keyed by exact upstream model; absent rows remain null. | [SharedProviderCatalogProjection.cs:155](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderCatalogProjection.cs:155) | Do not add another price catalog, infer vendor prices, or treat an absent price as zero. |
| Import materialization preserves catalog price fields, and runtime mapping rekeys them to the public routing model ID. | [SharedProviderRuntimeProfileMaterializer.cs:466](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs:466), [SharedProviderProfileMapper.cs:78](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedProviderProfileMapper.cs:78), [SharedProviderPriceMapper.cs:7](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderPriceMapper.cs:7) | Consumer pricing is a different path from publisher relay accounting. Preserve local profile ID, public routing model ID, publication ID and source identity; never match on a display name alone. |
| The existing calculator rejects an all-zero standard rate row. | [ProviderPricingModels.cs:27](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs:27), [TryFindPrice:366](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs:366) | Explicit free pricing needs a typed decision; absence, private/local execution and configured zero are not interchangeable. Do not silently change all legacy zero placeholders into free prices. |
| Current relay usage retains only input/output totals or image count. Cached input, cache writes and reasoning evidence are lost. | [SharedProviderRelayRuntimeContracts.cs:13](C:/repositories/CanDoItAll/src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderRelayRuntimeContracts.cs:13), [SharedProviderRelayPolicies.cs:270](C:/repositories/CanDoItAll/src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelayPolicies.cs:270), [relay projection:200](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedProviderRelayUsageProjectionSource.cs:200) | Calculating all input as uncached would produce incorrect precise-looking prices. Missing breakdown must remain explicit. |
| Existing integration characterization deliberately asserts unpriced relay rows. | [SharedProviderOpenAiCompatibilityIntegrationTests.cs:169](C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/SharedProviderOpenAiCompatibilityIntegrationTests.cs:169) | Replace this assertion for a priced fixture; retain a separate unconfigured-price fixture. Merely rerunning old tests would preserve the gap. |

Unverified runtime candidates for an **imported-consumer** row: source has no configured exact-model tariff; import snapshot predates price configuration; selected public model differs from the model stored in usage; usage is missing/incomplete; legacy record lacks execution price. Distinguish these from the confirmed publisher finalizer gap by workload and canonical source ID. A local request failing or a missing tariff must not trigger live inference or a catalog refresh automatically during history search.

## 2. Reuse existing ownership

`SharedProviderInvocationRecord` already owns a compact audit with provider profile/publication, public/upstream model, request/trace/correlation IDs, caller subject, access-context reference, start/completion/duration, outcome/failure, usage, price/completeness and expiration. See [record:6](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderInvocationRecord.cs:6).

Keep this row canonical for inbound relay calls. The neutral provider-history index may store bounded metadata and a typed reference to that row; it must not create a second standalone invocation or copy the transcript. Mark the lower outbound dispatch as owned by the relay audit so the generic recorder cannot count it again. A neutral typed source reference/suppression marker is appropriate; making generic Providers depend on ProviderManagement is not.

Provider/model is a filter and attribution key, not a unique request key. Index identity must include source kind and source record/attempt ID. Different calls to the same provider/model must remain distinct. For retries, operation correlation and attempt identity are different values.

The current usage projection performs an unbounded read and join of all invocation/profile rows before building contributions ([projection:29](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedProviderRelayUsageProjectionSource.cs:29)). Do not use it as the new history search backend. Use the neutral index with SQL-side range/filter/keyset paging; use existing aggregate projections only for their established summary consumers.

## 3. Caller and credential attribution before IDM

There is already enough trusted identity for basic per-key history:

- `ApiTokenService` creates a GUID per token, signs it as `jti`, and registers non-secret `ApiTokenRecord(Id, Subject, DisplayName, ...)`: [ApiAccess.cs:202](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiAccess.cs:202), [registry contract:9](C:/repositories/CanDoItAll/src/Foundation/CanDoItAll.Infrastructure/ControlPlane/ApiTokenRegistry.cs:9).
- The existing constant `ApiManagedTokenClaims.TokenId` is `jti`: [ApiScopeCatalog.cs:27](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiScopeCatalog.cs:27).
- JWT authentication validates issuer, audience, signature and lifetime before managed-token registry validation: [ApiServiceCollectionExtensions.cs:165](C:/repositories/CanDoItAll/src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs:165).
- Managed validation requires the supported version, a valid GUID token ID and an active registry record. A legacy signed JWT with no managed version is intentionally accepted subject to normal JWT/scope checks: [ApiManagedTokenValidation.cs:9](C:/repositories/CanDoItAll/src/App/CanDoItAll.Web/Api/ApiManagedTokenValidation.cs:9).
- The relay currently extracts only subject (`sub`, NameIdentifier or name); disabled authorization uses a constant subject: [SharedProviderInferenceApi.cs:419](C:/repositories/CanDoItAll/src/App/CanDoItAll.Web/Api/SharedProviderInferenceApi.cs:419).

### Required typed fields

| Field | Source and rule |
|---|---|
| Authentication kind | Explicit enum for managed token, legacy authenticated token, trusted local execution, authorization disabled, unavailable legacy evidence. No identity inferred from request content. |
| Caller subject + validated issuer snapshot | Read from the already validated principal. Subject is not assumed to be an IDM user or unique credential. |
| Nullable managed credential ID | Existing registry GUID from validated managed `jti`; distinct from subject and from the upstream provider's credential. No token value or hash of the bearer token. |
| Bounded credential label snapshot | Existing non-secret registry display name, if available during validated context creation. Historical display must survive registry deletion without a required join. Avoid a new registry file read for every result row. |
| Nullable upstream credential reference ID | Source provider's existing secret-reference ID if needed for operator troubleshooting. This identifies stored credential configuration, not a copied API key. |
| Existing access-context reference | Preserve as opaque caller-provided context, never authorization or authenticated client identity. |
| Source instance/publication/import identity | Capture exact imported relationship; local source-token secret ID and remote managed-token ID are different identity spaces. |

Create/map the trusted caller snapshot in Web, where authentication and token-registry knowledge already exist. Pass a small contract to the relay; neither generic Providers nor ProviderManagement should reference the Workspace token-administration module or `ClaimsPrincipal`/`HttpContext`. The neutral history contracts must not import the Infrastructure registry DTO.

Two managed credentials with the same subject must remain separately searchable. Rotated/revoked/deleted token records must not rewrite historic attribution. For legacy records/tokens without a managed ID, show the available subject plus explicit credential-unavailable status; do not fabricate a credential ID. Exact IDM user association stays outside this bundle.

## 4. Imported-client to publisher correlation without protocol changes

The publisher already emits its generated request ID in `CanDoItAll-Request-Id` ([SharedProviderApiResponseWriter.cs:38](C:/repositories/CanDoItAll/src/App/CanDoItAll.Web/Api/SharedProviderApiResponseWriter.cs:38)); the stored relay `RequestId` is the same server-owned trace identifier ([SharedProviderInferenceApi.cs:431](C:/repositories/CanDoItAll/src/App/CanDoItAll.Web/Api/SharedProviderInferenceApi.cs:431)).

Preferred minimal link: on the imported consumer, associate **local attempt ID + verified source instance ID + publisher response request ID** when the configured shared source returns headers. This requires a bounded response-header observer on the existing imported-provider HTTP path, not a new OpenAI request JSON field, synthetic SSE event or mandatory caller-supplied ID. The consumer does not receive permission to query the publisher's history merely by possessing the ID. A two-instance test must verify both directions and standard clients without these optional observations.

Trust limits:

- The current `AccessContextReference` is syntactically validated, not authenticated; a forged value cannot identify the client or collapse records ([middleware:13](C:/repositories/CanDoItAll/src/App/CanDoItAll.Web/Api/AccessContextReferenceMiddleware.cs:13), `SharedProviderAccessContextTests.ForgedAccessContext_DoesNotSatisfyAuthentication`).
- Traceparent/trace ID is diagnostic correlation, not a unique source record or authorization proof.
- Use the already validated/pinned shared source relationship and its network policy. A source using approved plain HTTP cannot provide cryptographic server authentication; label the link as observed correlation, not proof of user identity.
- Missing/malformed/duplicate response request IDs leave the link unavailable. Never guess by timestamp/model/subject. A failure before receiving headers legitimately has no publisher link.
- A returned publisher ID is evidence of a response, not success or completed usage.
- Do not automatically deduplicate two machines' independent accounting. Link them as two perspectives of a relay call; local source ownership controls local duplicate suppression.

Upstream request IDs are already sanitized for diagnostic logging in [SharedProviderHttpRelayClient.cs:39](C:/repositories/CanDoItAll/src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderHttpRelayClient.cs:39). If persisted, retain them separately from the publisher request ID and without upstream headers or response bodies.

## 5. Smallest correct pricing change

1. Resolve an immutable execution pricing snapshot from the exact source profile/model while that target is loaded, before dispatch. Include currency/unit, configured rates, pricing provenance/revision, and explicit availability. Capture the source catalog revision for imported requests. A price edit during an in-flight request must not alter its eventual price.
2. Extend repository-owned relay usage normalization/persistence for the cost-bearing token categories actually provided by the protocol. Keep nullable evidence distinct from zero. Preserve image counts and mark unsupported image/audio cost units explicitly.
3. Add a small top-level relay pricing policy/finalization helper in ProviderManagement (or an owner adapter over neutral history pricing). Reuse the arithmetic in `ProviderPricingCalculator` from Models; do not copy pricing formulae into Web, HTTP adapters, EF configuration or Razor.
4. Validate input evidence before calculation. The current calculator accepts `int` counts and clamps them ([calculator:750](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs:750)), while relay contracts store `long`. Require a checked conversion with explicit unsupported-range status, or a cohesive validated long-count overload. Never overflow or clamp an observed long count into a false price.
5. Persist the execution result and its snapshot/provenance. Separate calculated execution price, explicit free price, insufficient usage, missing tariff, unsupported cost unit and legacy missing evidence. A partial estimate must be visibly different from a known amount; unknown is not zero.
6. Keep current catalog/import mapping behavior and existing no-invented-prices tests. Do not backfill older relay prices from today's mutable tariff as if that tariff existed at execution. Any future historical estimate needs separate provenance and must not overwrite original evidence.

Cache/read/write categories must be disjoint in the arithmetic; reasoning tokens usually belong inside output counts and must not be charged twice. Long-context tariff choice uses the actual request context, not a sum across retry attempts. A private provider is not automatically free. The current all-zero placeholder convention needs an explicit free-pricing state/migration rule before zero prices can be accepted.

## 6. Lifetime, failure, retention and coverage constraints

### Preserve and test existing relay guarantees

- Invalid/unknown/unsupported requests are rejected before audit start; audit starts only after an eligible exact target and credentials resolve ([application service:50](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRelayApplicationService.cs:50)). Do not invent a provider for authentication/route rejection. Generic security rejection logging is a separate concern.
- Audit start is mandatory before upstream dispatch. An audit-start failure returns `AuditUnavailable`; preserve this explicit behavior ([application service:114](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRelayApplicationService.cs:114)).
- Completion is cached once per finalizer, with three bounded persistence attempts and a ten-second independent finalization timeout. Exhaustion throws and logs sanitized identifiers ([finalizer:47](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderAuditedRelayStream.cs:47)). These retries must never retry the provider request.
- Caller cancellation, timeout and upstream failure are separate paths. Streaming completion must wait for terminal protocol evidence, not merely HTTP 200/headers or enumeration construction.
- The SSE reader carries last observed usage into abandonment/failure ([SharedProviderSseRelayStream.cs:166](C:/repositories/CanDoItAll/src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderSseRelayStream.cs:166), [finally:211](C:/repositories/CanDoItAll/src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderSseRelayStream.cs:211)). The outer audited wrapper also initiates cancellation with unavailable usage ([wrapper:203](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderAuditedRelayStream.cs:203)). Characterize this completion/disposal race before changing it; first-finalizer-wins can otherwise discard better observed evidence.
- Recovery selects stale in-progress records in bounded pages; it does not replay upstream calls ([recovery:15](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderInvocationRecoveryService.cs:15)). Current recovery marks failure/unavailable, not proof that upstream execution never occurred ([transitions:116](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderInvocationTransitions.cs:116)). History must identify interrupted finalization/unknown billing rather than claim no usage.

Retention is hard-coded to 30 days ([application service:25](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRelayApplicationService.cs:25)). `DeleteAfterUtc` has an index, but the inspected source tree has no purge implementation for relay records. Recovery is not retention cleanup. Add validated general settings and bounded cleanup using canonical ownership. Do not delete in-progress requests or leave searchable detail/index references after their owner has expired.

Existing indexes cover request uniqueness, publication/start and expiration/completion; the publication/provider foreign key restricts deletion ([configuration:79](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderInvocationRecordConfiguration.cs:79)). New index rows need immutable names/identity snapshots and optional references; do not introduce a cascade that erases history when a provider/token is deleted. Migration must coordinate canonical records, index repair and detail cleanup.

### Request-capture seam: no universal decorator claim

A generic runtime-handle decorator alone is insufficient; a driver-factory decorator alone is also insufficient.

| Production path | Actual boundary and coverage requirement |
|---|---|
| Buffered provider-backed simple chat | [ProviderBackedLlmInvocationAdapter.cs:38](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmInvocationAdapter.cs:38) retries a new driver dispatch and aggregates usage. Capture each attempt below that retry loop; retain one logical operation link. |
| Streaming provider-backed simple chat | [ProviderBackedLlmStreamingInvocationAdapter.cs:266](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmStreamingInvocationAdapter.cs:266) consumes the full stream inside a dispatch callback, whose return value is only `true`. A generic result observer cannot recover usage; use a typed stream adapter and do not buffer text for light logs. |
| MAF SDK chat | [MafProviderAgentFactory.cs:412](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderAgentFactory.cs:412) wraps SDK `IChatClient` in transport policy and then empty-response retry. [MafProviderTransportBoundaryChatClient.cs:146](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderTransportBoundaryChatClient.cs:146) uses a lane gate, not a runtime handle or typed driver. Add a small dedicated history `IChatClient` decorator inside application retry, not more logic to that large transport class. |
| Shared relay | [AgentFrameworkProviderRuntimeGateway.cs:163](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Providers/AgentFrameworkProviderRuntimeGateway.cs:163) returns transport response at headers; body/stream completes later. Retain the existing relay audit owner and suppress duplicate lower-dispatch history. |
| Batch items | [ProviderBatchJobBalancer.cs:268](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Providers/Batching/ProviderBatchJobBalancer.cs:268) retries with the same input ID as runtime correlation. Require fresh attempt IDs plus job/item/attempt-number association; recovered completed checkpoints ([line 73](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Providers/Batching/ProviderBatchJobBalancer.cs:73)) must create no new billable attempt. |
| Image and voice | [ProviderRuntimeImageGenerationService.cs:34](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Images/ProviderRuntimeImageGenerationService.cs:34), [ProviderRuntimeVoiceDriver.cs:24](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Voice/ProviderRuntimeVoiceDriver.cs:24) use runtime handles and typed drivers. Include metadata for generate/edit image, speech transcription and synthesis; body capture must not inline audio/image bytes. |
| Health/catalog/maintenance | These are real provider operations. OpenAI health internally lists models then invokes chat ([OpenAiProviderDriver.cs:69](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Providers/Drivers/OpenAiProviderDriver.cs:69)); a decorator on the outer health method misses its internal probe. Record transport attempts where present and use a parent diagnostic operation; avoid one synthetic extra billable health record. |

Use a small neutral per-attempt recorder API with explicit typed adapters at these existing boundaries. A handle decorator may coordinate context and queue timing, but must not be the sole usage/body/terminalization observer. No reflection/object payload classifier, generic plugin framework, or new runtime partial is justified.

The first-version matrix must cover the existing closed `AgentProviderOperationKind` set ([ProviderDispatchModels.cs:14](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderDispatchModels.cs:14)): ListModels, CompleteChat, AnalyzeImage, GenerateImage, EditImage, TranscribeSpeech, SynthesizeSpeech, TestHealth and CreateOrUpdateModel. Separate operational requests from billable inference. No embedding invocation/capability was found in the inspected main repository; sibling RAG/provider scope needs its own evidence before being claimed covered.

Distinguish application/provider-call attempts from hidden SDK/HTTP retries. If first-version capture stays above an SDK retry policy, label that granularity explicitly; do not promise one row per network transmission. The neutral model can represent child transport attempts when an existing transport observer exposes them, without copying response text per retry.

## 7. Responsibility and dependency guard

Current relevant references were read from project files:

- `SharedProviders.Abstractions`: standalone, no project references.
- `SharedProviders.Http` -> SharedProviders.Abstractions, AgentFramework.Models, AgentFramework.Providers.
- `Modules.AgentFramework.ProviderManagement` -> Core, Models, Providers, Infrastructure, SharedKernel, Modules.Security, SharedProviders.Abstractions.

Retain these directions. Proposed neutral ProviderHistory Abstractions/Application/Persistence projects may supply contracts, query policy and index storage, but neutral contracts must not point back to ProviderManagement, Workspace, MAF SDKs, Web or EF. Outer owner adapters map their existing data. A new neutral project is justified by shared query/capture lifecycle, not by moving unrelated contracts into a common dumping ground.

Current hotspots: `SharedProviderRelayApplicationService` is 474 lines with 11 constructor dependencies; `SharedProviderAuditedRelayStream.cs` contains a 173-line persistence finalizer plus the stream wrapper; `ProviderPricingModels.cs` contains the existing large pricing utility group. Do not add search, content retention, token-registry lookup or formatting to these types. New pricing/caller/capture behavior belongs in cohesive top-level types. If touching the audit finalizer, place it in its own file as part of the responsibility split, not a new partial class.

## 8. Focused proof plan

Existing test homes, not new test-file placeholders:

| Existing class | Extend/retain proof |
|---|---|
| `SharedProviderPublicationAndCatalogTests` | Exact published rates, no invented defaults, duplicate upstream names, price revision changes without route change. |
| `SharedProviderProtocolContractTests` | Every tariff field survives serialization and revision; invalid/negative rates rejected. |
| `SharedProviderRuntimeProfileMaterializerTests` | Public routing ID price key, imported source metadata preserved, legacy snapshot requires resync. |
| `ProviderPricingTests` | Cache/write/long-context arithmetic, validated long conversion, missing tariff vs explicitly free tariff, no current-price rewrite of old records. |
| `SharedProviderRelayPolicyTests`, `SharedProviderStateModelTests` | Usage evidence/completeness invariants, metadata-only light mode, idempotent terminalization and recovery. |
| `SharedProviderPersistenceIntegrationTests` | Immutable caller/pricing snapshots, concurrent finalize, schema constraints/indexes, expiry and canonical/index ownership. |
| `SharedProviderOpenAiCompatibilityIntegrationTests` | Priced buffered fixture via real application/audit path, separate unpriced fixture, same-subject/different-token ID attribution, no private data upstream; existing `PersistedProviderRelay_ResolvesRouteSecretAndFinalizesMetadataOnlyAudit` is the natural extension. |
| `SharedProviderStreamingIntegrationTests` | Incremental first chunk, cached token usage, timeout/cancel/error with last evidence, abandonment race and at-most-once finalization. |
| `SharedProviderBackendCheckpointIntegrationTests` | Two real local hosts and deterministic upstream, existing `Both_streaming_surfaces_are_incremental_terminal_and_cancel_upstream` and `Access_context_is_validated_audited_not_forwarded_and_audit_is_content_free`. Add consumer-to-publisher response-ID link and exactly one canonical publisher row. |
| `ApiTokenRegistryTests`, `ApiAccessAuthorizationIntegrationTests`, `SharedProviderAccessContextTests` | Revoked/deleted/legacy keys, unavailable identity, spoofed context cannot grant access, query authorization separated from attribution. |
| `ProviderRuntimeContractOwnershipTests`, `LlmInvocationPortCompositionTests`, `LlmChatProviderResolutionTests`, `LlmChatRuntimeFenceTests` (actual classes in `LlmChatProviderRuntimeTests.cs`), `MafProviderTransportBoundaryChatClientTests`, `EmptyCompletionRetryChatClientTests`, `ProviderRuntimeLifecycleTests`, `ProviderRuntimeImageGenerationServiceTests`, `AgentVoiceTests` | Each existing invocation family emits bounded neutral metadata; retries have distinct attempts, recovered batches emit none, relay does not double-write. |

Focused future commands (not executed during preparation):

```powershell
dotnet test C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~SharedProviderPublicationAndCatalogTests|FullyQualifiedName~SharedProviderProtocolContractTests|FullyQualifiedName~SharedProviderRuntimeProfileMaterializerTests|FullyQualifiedName~SharedProviderRelayPolicyTests|FullyQualifiedName~SharedProviderStateModelTests|FullyQualifiedName~ProviderPricingTests|FullyQualifiedName~ApiTokenRegistryTests"
dotnet test C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~SharedProviderPersistenceIntegrationTests|FullyQualifiedName~SharedProviderOpenAiCompatibilityIntegrationTests|FullyQualifiedName~SharedProviderStreamingIntegrationTests|FullyQualifiedName~SharedProviderBackendCheckpointIntegrationTests|FullyQualifiedName~ApiAccessAuthorizationIntegrationTests|FullyQualifiedName~SharedProviderAccessContextTests"
```

Also select/run the affected runtime/MAF/image/voice/batch tests after concrete edits, using actual changed-line impacted-test analysis. A static source assertion that a decorator is registered is insufficient: send fake requests through production composition and assert the emitted rows, unchanged transcript ownership, failure outcomes and zero secret/body disclosure in light mode.

## 9. Preparation exit

This analysis establishes code-level causes, existing metadata ownership, trusted credential identity, protocol-compatible request correlation, lifetime hazards and executable test homes. Build/test/benchmark/migration results are intentionally not claimed: this is a preparation bundle. Runtime confirmation of the reported localhost row, exact affected tests after implementation, query plans, retention repair, and deterministic two-instance verification remain execution gates.

