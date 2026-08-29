# SB04 — Invocation Capture And Caller Attribution

## Status

- Execution: Completed

## Objective

- Capture every identified application-visible provider-call attempt at its real typed path, with verified caller identity, frozen price and durable ownership, without changing invocation behavior.

## Covered Inputs

- N001, N002, N006, N007, N009–N011; R001, R002, R006, R007, R009–R011, R014.
- [Normalized requirements](../../requirements/01-normalized-requirements.md).

## Prerequisites

- SB02 price and SB03 durable-store gates passed, including SB01 capture matrix.
- Inspect actual production factories and retry/decorator ordering again after concurrent edits.
- Deterministic fake upstreams cover buffered/SSE/image/speech/retry/cancellation; no paid model call.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmInvocationAdapter.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmStreamingInvocationAdapter.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderAgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderTransportBoundaryChatClient.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Images/ProviderRuntimeImageGenerationService.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Voice/ProviderRuntimeVoiceDriver.cs`
- `repo://src/App/CanDoItAll.Web/Api/ApiManagedTokenValidation.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiScopeCatalog.cs`
- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/05-history-data-lifecycle.md`
- `bundle://architecture/09-search-security-contract.md`
- `bundle://architecture/10-pricing-and-capture-contract.md`

Linked source context:

[Buffered LLM adapter](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmInvocationAdapter.cs).
[Streaming LLM adapter](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime/ProviderBackedLlmStreamingInvocationAdapter.cs).
[MAF provider factory](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderAgentFactory.cs).
[MAF transport boundary](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderTransportBoundaryChatClient.cs).
[Image generation](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Images/ProviderRuntimeImageGenerationService.cs).
[Voice driver](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Voice/ProviderRuntimeVoiceDriver.cs).
[Managed-token validation](C:/repositories/CanDoItAll/src/App/CanDoItAll.Web/Api/ApiManagedTokenValidation.cs).
[API scope/claim constants](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiScopeCatalog.cs).
Normative [boundary map](../../architecture/01-csharp-boundary-map.md),
  [lifecycle](../../architecture/05-history-data-lifecycle.md),
  [query/security](../../architecture/09-search-security-contract.md) and
  [pricing/capture](../../architecture/10-pricing-and-capture-contract.md).

## Deliverables

- Add small typed buffered/streaming/MAF/batch/image/voice/operational observers at actual dispatch/terminal boundaries, sharing one neutral lifecycle policy.
- Allocate logical and actual attempt IDs explicitly; preserve trusted pending canonical ownership across retries and source commits.
- Map validated managed credential GUID/issuer/subject in Web; relay audit keeps canonical begin/finalize and projects once.
- Preserve execution price snapshot, nullable observed categories, canonical/provider-reported precedence and explicit SDK-internal retry limits.
- Capture only permitted bounded current-turn detail with shared input per operation; arbitrary unsupported relay shapes remain metadata-only with a reason.
- Carry optional configured-source/observed publisher request-ID relation without changing required JSON/SSE or using it for authorization.

## C# Architecture Impact

Typed adapters stay in existing runtime/SDK owners. MAF uses one dedicated IChatClient decorator inside application retry; generic handle wrappers cannot serve as universal terminal observers.

## Boundary Ownership

Web owns verified caller identity; ProviderManagement owns inbound relay audit; existing chat/agent/workflow producers declare content ownership before dispatch. Never accept owner/attempt markers from untrusted headers as authority.

## Dependency Direction

Runtime producers consume neutral history ports only. ProviderManagement does not reference Workspace token services; SharedProviders.Abstractions remains independent and maps its own caller DTO outward.

## Pattern Decision

ADR02/04/06: typed adapters/decorator, stable per-attempt identity and bounded optional detail. Retain existing retry, batching, queue, tool and approval behavior.

## Testability Contract

Extend existing runtime/MAF/image/voice/relay/auth fixtures. Proposed cases: Durable_start_failure_sends_nothing; Empty_retry_has_two_attempts_one_operation; Stream_terminal_usage_survives_disposal; Managed_keys_with_same_subject_remain_distinct; Terminal_write_failure_does_not_repeat_inference; Recovered_batch_result_creates_no_attempt.

## Partial Class Policy

No new runtime partial. Existing Razor code-behind/generated files are exceptions only for
their established framework role. New cohesive classes follow the 250-line review and
400-line redesign/exception gate; extraction removes the original behavior.

## Architecture Proof Required

- Record actual changed files, public signatures and project edges against the allowed
  dependency table. Review DI factories and old call sites, not only the new collaborator.
- Exercise the actual production factory/backend composition, show old direct paths replaced exactly once, and verify no object/TResult-based usage inference or duplicated relay observation.

## Dependency Impact

- SB05 consumes trusted attempt/owner mappings; SB06 relies on safe caller/model/price facts.
- Any uncovered production path or duplicate nested observation reopens SB01 matrix and blocks completeness/UI release.

## Validation Depth

- Proof tier: `Governed`.
- Critical foundation: Yes; all actual invocation paths, trustworthy caller identity and no duplicate inference/audit..
- Test project/filter: `C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` / `FullyQualifiedName~ProviderRuntimeContractOwnershipTests|FullyQualifiedName~LlmInvocationPortCompositionTests|FullyQualifiedName~LlmChatProviderResolutionTests|FullyQualifiedName~LlmChatRuntimeFenceTests|FullyQualifiedName~ProviderBackedLlmInvocationAdapterTests|FullyQualifiedName~ProviderBatchJobBalancerTests|FullyQualifiedName~ProviderRuntimeLifecycleTests|FullyQualifiedName~MafProviderTransportBoundaryChatClientTests|FullyQualifiedName~MafProviderAgentFactoryEmptyCompletionCompositionTests|FullyQualifiedName~ProviderRuntimeImageGenerationServiceTests|FullyQualifiedName~ProviderRuntimeImageAnalysisServiceTests|FullyQualifiedName~AgentVoiceTests|FullyQualifiedName~ApiTokenRegistryTests|FullyQualifiedName~ProviderHistoryCaptureTests`; `C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` / `FullyQualifiedName~SharedProviderOpenAiCompatibilityIntegrationTests|FullyQualifiedName~SharedProviderStreamingIntegrationTests|FullyQualifiedName~SharedProviderAccessContextTests|FullyQualifiedName~SharedProviderAuthorizationIntegrationTests|FullyQualifiedName~ApiAccessAuthorizationIntegrationTests`.
- Selection reason: Actual buffered retry and batch attempt producers, runtime lifetime/fencing, SDK production composition, image generation/analysis, voice, real managed-token HTTP validation and shared stream/authorization paths. LlmChatProviderRuntimeTests.cs contains several classes; the selectors use their actual contract/composition/resolution/fence names, not the filename. ProviderHistoryCaptureTests is proposed and must be discovered after implementation.
- Expected discovery: ForgedAccessContext_DoesNotSatisfyAuthentication, PersistedProviderRelay_ResolvesRouteSecretAndFinalizesMetadataOnlyAudit, InvokeAsync_aggregates_usage_across_empty_and_successful_attempts, ProviderBatchBalancer_CheckpointedRecoverySkipsCompletedItemsAndRetriesFailures, RuntimeLifecycle_DoesNotCaptureScopedServices, AnalyzeAsync_maps_gateway_request_and_preserves_token_usage, TOKEN_LIFECYCLE_deleted_and_revoked_tokens_fail_real_http_requests and TOKEN_LIFECYCLE_legacy_tokens_remain_subject_to_signature_and_scope_checks, plus six proposed capture cases and named coverage cases for every matrix row. Record exact actual cases/counts at execution;
  zero discovery or a missing named expected case fails the gate. Discovery has not run now.
- Invalidation keys: ProductionCaptureWiring; AttemptGranularity; TrustedCallerSnapshot; StreamTerminalPolicy; RetrySemantics; DetailInputContract.
- Broad-gate decision: Required once at frozen SB08 only if public-contract/schema/DI
  changes made here trigger it. No broad suite here or repeated run without invalidation.
- Future focused commands (after implementing the named cases; use the same unchanged
  source revision for discovery/build and the subsequent no-build execution):

```powershell
dotnet test 'C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj' --list-tests --filter 'FullyQualifiedName~ProviderRuntimeContractOwnershipTests|FullyQualifiedName~LlmInvocationPortCompositionTests|FullyQualifiedName~LlmChatProviderResolutionTests|FullyQualifiedName~LlmChatRuntimeFenceTests|FullyQualifiedName~ProviderBackedLlmInvocationAdapterTests|FullyQualifiedName~ProviderBatchJobBalancerTests|FullyQualifiedName~ProviderRuntimeLifecycleTests|FullyQualifiedName~MafProviderTransportBoundaryChatClientTests|FullyQualifiedName~MafProviderAgentFactoryEmptyCompletionCompositionTests|FullyQualifiedName~ProviderRuntimeImageGenerationServiceTests|FullyQualifiedName~ProviderRuntimeImageAnalysisServiceTests|FullyQualifiedName~AgentVoiceTests|FullyQualifiedName~ApiTokenRegistryTests|FullyQualifiedName~ProviderHistoryCaptureTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj' --no-build --filter 'FullyQualifiedName~ProviderRuntimeContractOwnershipTests|FullyQualifiedName~LlmInvocationPortCompositionTests|FullyQualifiedName~LlmChatProviderResolutionTests|FullyQualifiedName~LlmChatRuntimeFenceTests|FullyQualifiedName~ProviderBackedLlmInvocationAdapterTests|FullyQualifiedName~ProviderBatchJobBalancerTests|FullyQualifiedName~ProviderRuntimeLifecycleTests|FullyQualifiedName~MafProviderTransportBoundaryChatClientTests|FullyQualifiedName~MafProviderAgentFactoryEmptyCompletionCompositionTests|FullyQualifiedName~ProviderRuntimeImageGenerationServiceTests|FullyQualifiedName~ProviderRuntimeImageAnalysisServiceTests|FullyQualifiedName~AgentVoiceTests|FullyQualifiedName~ApiTokenRegistryTests|FullyQualifiedName~ProviderHistoryCaptureTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj' --list-tests --filter 'FullyQualifiedName~SharedProviderOpenAiCompatibilityIntegrationTests|FullyQualifiedName~SharedProviderStreamingIntegrationTests|FullyQualifiedName~SharedProviderAccessContextTests|FullyQualifiedName~SharedProviderAuthorizationIntegrationTests|FullyQualifiedName~ApiAccessAuthorizationIntegrationTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj' --no-build --filter 'FullyQualifiedName~SharedProviderOpenAiCompatibilityIntegrationTests|FullyQualifiedName~SharedProviderStreamingIntegrationTests|FullyQualifiedName~SharedProviderAccessContextTests|FullyQualifiedName~SharedProviderAuthorizationIntegrationTests|FullyQualifiedName~ApiAccessAuthorizationIntegrationTests'
```

## Implementation Steps

1. Implement each matrix adapter with a durable reservation before actual send; explicitly pass trusted context.
2. Wire the MAF decorator at the verified factory position and instrument typed stream terminal events without body buffering.
3. Use existing relay canonical begin/finalize, same-context projection and trusted Web caller snapshot.
4. Add batch retry/recovery, image/speech and diagnostic granularity coverage; no fabricated embedding coverage.
5. Verify terminal cancellation/persistence races, input bounds, secret exclusions and production factory completeness.

## Acceptance Checklist

- [x] Each observed actual call has one attempt; no duplicate relay standalone record or recovered-result inference.
- [x] Buffered and streaming execution keep current latency/lifetime/cancellation and tool/approval semantics.
- [x] Verified keys with equal subject remain distinguishable; legacy/unavailable/auth-disabled attribution is honest.
- [x] No credentials, assembled prior conversation or binary media persist in metadata/detail.
- [x] Operational/health granularity and unobserved SDK retries are explicitly limited; no fabricated free calls.
- [x] Terminal persistence failure never schedules another model call.

## Proof Required

- Store a proof manifest, exact command transcripts, discovered cases/exit codes, changed-source revision, artifact paths/hashes and semantic positive/negative evidence under `proof/SB04/` at the bundle root.
- Produce a coverage matrix with actual production producer call sites, named passing positive/negative tests and per-attempt expected row counts for every operation kind. Include first-chunk/cancellation and safe caller-identity assertions; use fake providers only.
- Follow [validation strategy](../../plan/02-validation-strategy.md); distinguish existing
  test anchors from proposed new cases, and source proof from executed behavior.

## Browser Validation Logging

N/A for direct UI changes in this phase. Production host/SQL/lifecycle proof is required where listed; the two-tab desktop acceptance remains SB07/SB08.

## Scope Exceptions

- This phase alone does not close the complete product request. Deferred IDM/EGCP person
  mapping, global federation, exact wire replay, mobile redesign and unrelated refactors
  remain outside the bundle.
- No paid inference, user-database mutation or deployment without explicit authorization.

## Do Not Do

- Do not use a universal payload walker, ambient magic-string context, or a new runtime.
- Do not treat headers/first chunk/HTTP200 as completed streaming use.
- Do not accept caller hints as permission, copy all messages, or claim exact SDK HTTP retry accounting.

## Progression Gate

- SB05 may consume capture only after all identified paths, trusted attribution and durable/terminal failure cases pass. No two-tab feature release with a partial hidden capture matrix.
- Update [execution report](../../reviews/01-execution-report.md) with actual proof and
  downstream dependencies checked. A planned command or passed intermediary is not closure.

## Reopen Triggers

- Factory/decorator/retry changes, new operation kinds, caller-validation changes or missing/duplicate terminal observations invalidate capture and downstream indexing/search proof.
