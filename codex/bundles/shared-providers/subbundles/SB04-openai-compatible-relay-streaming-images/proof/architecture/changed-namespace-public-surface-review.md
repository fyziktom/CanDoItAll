# SB04 changed namespace, public surface, and partial-class review

State: `PASS`.

## Namespace and ownership decisions

| Owner | SB04 responsibility | Review decision |
| --- | --- | --- |
| `CanDoItAll.SharedProviders.Abstractions` | neutral target/request/result, usage, failure/header, streaming, dispatcher/application, and image-capability contracts | remains SDK-, EF-, ASP.NET-, product-entity-, and HttpClient-free |
| `CanDoItAll.SharedProviders.Http` | exact request normalization, connector URI ownership, typed dispatch, five-row adapter registry, header/error/usage policy, and bounded SSE parsing | references only Abstractions; concrete client/adapters/stream stay internal |
| `CanDoItAll.Modules.Workspace` | current publication/profile/model/secret resolution, target creation, metadata audit, idempotent finalization, stale-row recovery, and image execution target | owns persistence and secret lifecycle; depends on neutral ports, never Http implementation |
| `CanDoItAll.Modules.AgentFramework` | narrow bridge to the existing image capability and existing usage direction | uses its authorized outer Workspace boundary plus Abstractions; no current-state image persistence query remains |
| `CanDoItAll.AgentFramework.Usage` | existing provider-usage contracts and aggregation | gains additive image-count metadata without a second ledger or breaking primary constructors |
| `CanDoItAll.Web` | three POST routes, bounded read, authorization, OpenAI envelopes, safe buffered/SSE output, and marker-scoped OpenAPI | endpoint/OpenAPI types remain internal and delegate to the application port |
| `CanDoItAll.Composition` | concrete Http registration | remains the product composition owner of the Http implementation |

## New SB04 public relay surface

| Role | Public declarations | Decision |
| --- | --- | --- |
| relay values/results | `SharedProviderRelayUsage`, response headers, credential, target, normalized request, request context, application/dispatch requests, dispatch result | required cross-project runtime contract; constructors enforce bounds/coherence and expose no product/EF/ASP.NET type |
| streaming | `SharedProviderRelayStreamFrame`, `SharedProviderRelayStreamCompletion`, `ISharedProviderRelayStream` | bounded SDK-free async surface with explicit completion, usage, failure, cancellation, and disposal |
| runtime ports | `ISharedProviderRelayRequestPolicy`, `ISharedProviderRelayDispatcher`, `ISharedProviderRelayApplicationService` | minimal real inversion between Web, Workspace, and Http |
| image bridge | `SharedProviderImageCapabilityRequest`, `SharedProviderGeneratedImage`, `ISharedProviderImageCapabilityRelay` | in-process bridge carrying publication/profile/model identity and bytes, never a private path/URL or Workspace entity |
| capability mapping | `SharedProviderRelayCapabilityMap` | one typed mapping rather than repeated magic identifiers |
| Http deterministic policy | request, URI, failure, response-header, and usage-extractor policy classes | stateless policy directly exercised by focused tests; client/adapters/stream stay internal |
| Http registration | expanded `AddSharedProviderHttpDescriptors` | preserves the existing composition extension while hiding implementation types |
| Workspace application | `SharedProviderRelayApplicationService` | concrete neutral-port implementation; current persisted state and secrets stay in Workspace |
| Workspace image seam | `SharedProviderImageExecutionTarget`, `ISharedProviderImageExecutionTargetResolver` | necessary existing outer-module seam; detached target never enters Web/Http/protocol output |

`SharedProviderRelaySupportCatalog` remains the existing public support-port implementation and now
backs real dispatch. Its Production rows are exactly OpenAI chat, OpenAI image, Ollama local chat,
Ollama remote chat, and ComfyUI image. None advertises vision input.

## Five additive image-count surfaces and ABI review

| Surface | Shape and compatibility decision |
| --- | --- |
| `SharedProviderRelayUsage.ImageCount` | read-only positive bounded count in the new SB04 relay value; mutually exclusive with token usage when complete |
| `SharedProviderInvocationCompletion.ImageCount` | public init-only additive property; existing 8-parameter primary constructor and 8-value deconstruction remain unchanged |
| `SharedProviderInvocationRecord.ImageCount` | public persisted nullable scalar; contains count metadata only and is operation-constrained in C#/PostgreSQL |
| `ProviderUsageContribution.ImageCount` | public init-only additive property; existing 16-parameter primary constructor/deconstruction remain unchanged |
| `ProviderUsageTotals.ImageCount` | public init-only additive aggregate; existing 10-parameter primary constructor/deconstruction remain unchanged |

The constructor/deconstruct arities are asserted by tests. This avoids a source/binary-shape break
for existing positional record consumers while allowing the new image metric. Image count is not
folded into token totals. Chat/Responses cannot persist/project images; Images cannot persist/
project tokens. Invalid operation/count mixtures fail transitions, PostgreSQL constraints, or
projection instead of being silently normalized.

## Internal testability seam review

`SharedProviderInvocationRecoverySchedule` is an internal immutable record. Production registration
uses its 10-second startup and one-minute reconciliation defaults. The integration fixture receives
friend access through `Properties/AssemblyInfo.cs` and replaces the schedule with 100 ms/100 ms.
This is a bounded deterministic timing seam, not a public product option or service-locator hook.
The hosted worker and PostgreSQL store used by the proof remain production implementations.

## Encapsulation review

The concrete Http client, adapter interface/key/registry, connector adapters, SSE parser/session,
connector keys, and timeout constants are internal. Web endpoint/OpenAPI/SSE writer types are
internal. AgentFramework image/usage implementations are internal. Workspace audit finalizer,
audited stream, recovery service/worker/schedule, and resolver implementation are internal.

No public HTTP DTO exposes `ProviderProfile`, `SecretRecord`, `DbContext`, `HttpContext`, an
upstream URI, or credential material. Access context and subject do not enter Http contracts. The
Workspace image target contains a detached profile only across the pre-existing outer
Workspace-to-AgentFramework relationship and is never serialized.

## Partial-class, cohesion, and dependency review

No partial class was introduced or extended. Request policy, URI/error/header/usage policy,
adapter registry, client, SSE session, Web routes, Workspace orchestration/audit/recovery/image
resolution, AgentFramework bridges, and usage aggregation remain cohesive top-level types. No
provider switch was added to Web/Workspace; no reflection bridge or service location was added.

Refreshed snapshot `snap-20260825051057-300644c7` covers 14 projects, 752 documents, 35 modules,
5,158 dependency edges, and 34 direct references. It reports zero project cycles, the governed two
module/one type cycles, and zero error findings. The reference artifact confirms no reverse edge.
