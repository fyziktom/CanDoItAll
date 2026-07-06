# Generic Memory Provider Authoring

Generic memory providers integrate through strongly typed profiles, manifests, operation envelopes, and driver interfaces. Do not add native Cognitive Memory, Qdrant, OpenAI, or host EF dependencies to generic memory, MAF, or the base composition root.

## Authoring Rules

1. Start with a `MemoryProviderProfile`.
2. Declare only capabilities that the driver and provider actually support.
3. Route all work through `IMemoryOperationHandler` or `IMemoryRuntimeService`.
4. Use Source Gateway snapshots for host data. Do not expose `AppDbContext` or module EF entities to a provider.
5. Persist observable work through the generic ledgers.
6. Return explicit unsupported, unavailable, timeout, provider-error, or no-provider results. Do not add fallback behavior that hides misconfiguration.
7. Keep provider-specific behavior behind a driver, MCP adapter, HTTP service, native service, or provider UI surface.

## Main Contracts

| Contract | Location | Purpose |
| --- | --- | --- |
| `MemoryProviderProfile` | `src/Memory/CanDoItAll.Memory.Abstractions` | Provider identity, driver kind, enablement, selection policy, and manifest. |
| `MemoryProviderManifest` | `src/Memory/CanDoItAll.Memory.Abstractions` | Protocol version, provider kind, capabilities, interaction support, UI surfaces, limits, and extensions. |
| `IMemoryProviderProfileStore` | `src/Memory/CanDoItAll.Memory.Application` | Profile persistence boundary. |
| `IMemoryProviderDriver` | `src/Memory/CanDoItAll.Memory.Application` | Context query dispatch boundary. |
| `IMemoryProviderHealthDriver` | `src/Memory/CanDoItAll.Memory.Application` | Provider health boundary. |
| `IMemoryProviderOperationStatusDriver` | `src/Memory/CanDoItAll.Memory.Application` | Async status polling boundary. |
| `IMemoryProviderFeedbackDriver` | `src/Memory/CanDoItAll.Memory.Application` | Feedback delivery boundary. |
| `IMemoryProviderEventPollDriver` | `src/Memory/CanDoItAll.Memory.Application` | Provider event polling boundary. |
| `IMemoryProviderEventOutboxDriver` | `src/Memory/CanDoItAll.Memory.Application` | Event acknowledgement/outbox boundary. |
| `IMemorySourceGatewayAdapter` | `src/Memory/CanDoItAll.Memory.Application` | Module-owned Source Gateway adapter boundary. |

## Driver Choice

Use an existing driver when possible:

| Need | Driver |
| --- | --- |
| Plain HTTP context query and health | `Http` |
| MCP tool-backed query, ingestion, feedback, events, or status | `Mcp` |
| Native Cognitive Memory remote service | `NativeRemote` |
| Deterministic tests and demos | `Mock` |

Add a new driver only when the transport cannot be represented by the existing HTTP or MCP drivers. A new driver must implement the narrow driver interfaces it actually supports and must be registered explicitly.

## Profile Manifest Checklist

A usable profile must include:

- stable `MemoryProviderInstanceId`;
- supported `MemoryProviderDriverKind`;
- `IsEnabled = true` for dispatch;
- `MemoryProtocolVersion.Current`;
- provider kind using a dotted lowercase id, such as `provider.vendor.memory`;
- accurate `MemoryCapabilityDescriptor` rows;
- matching `MemoryProviderInteractionSupport` flags;
- realistic `MemoryProviderLimits`;
- extension keys using `host.candoitall.*`, `native.cognitiveMemory.*`, or `provider.vendor.*`.

Keep `MemoryProviderFallbackBehavior.DenyImplicitFallback` unless a reviewed product path explicitly permits default-provider selection.

## Source Gateway

Source data must move through `MemorySourceSnapshot` contracts from `src/MAF/Common/CanDoItAll.AgentFramework.Core/Sources/MemorySourceSnapshotContracts.cs`.

When adding a new source adapter:

1. Define the source descriptor and snapshot mapping in the owning module.
2. Redact or exclude sensitive data before creating snapshots.
3. Preserve source ids, labels, provenance, citations, and timestamps.
4. Register the adapter with `AddMemorySourceGatewayAdapter<TAdapter>()`.
5. Add positive and negative tests for allowed source capture, missing source ids, and sensitive-data behavior.

Do not pass module EF entities, tracking queries, or raw DbContext instances to providers.

## Dispatch And Ledgers

All memory work must go through the shared handler:

- context queries: `IMemoryRuntimeService` or `IMemoryOperationHandler.ExecuteContextQueryAsync`;
- manual/source ingestion: `ManualMemorySourceIngestionService` or the source-capture path on `IMemoryOperationHandler`;
- feedback: `IMemoryOperationHandler.SubmitFeedbackAsync`;
- status/cancel: `IMemoryOperationHandler.GetOperationStatusAsync` and `CancelOperationAsync`;
- events: `IMemoryProviderEventWorker` plus event acknowledge paths.

Do not duplicate dispatch code in UI, MAF tools, workflows, or providers. The operation, feedback, source request, event inbox/outbox, retention, and health records are the production observability surface.

## Provider UI

Provider UI is optional and must remain safe when unavailable:

- RCL surfaces require a registered component key and `ui.rcl`.
- iframe or external URL surfaces require `ui.iframe` and a safe HTTPS or loopback HTTP URL.
- missing component registrations, missing URLs, unsafe URLs, disabled profiles, unhealthy profiles, or missing capabilities must render an explicit unavailable diagnostic.

Provider UI must not bypass generic runtime policy or call native services directly from the base host.

## Tests Required

For a new provider or driver, add focused proof at the narrowest layer:

- profile validation and selection tests;
- driver success, timeout, provider-error, unsupported-capability, and unavailable tests;
- ledger tests proving status transitions and diagnostics;
- Source Gateway tests when source capture is added;
- worker tests when async status, feedback, events, or outbox acknowledgements are supported;
- component or Playwright proof when profile setup or provider UI changes browser-visible behavior.

For native service changes, also run the native repository build/tests in `C:\repositories\CanDoItAll.CognitiveMemory`.

## Anti-Patterns

Reject these in review:

- direct references from generic memory, MAF, base composition, or `/memory` UI to `CanDoItAll.Modules.CognitiveMemory`;
- Qdrant, SemanticCompletion, OpenAI, or native DB registration as a base startup requirement;
- mock providers enabled by default;
- stringly typed provider ids or capability ids outside protocol construction/parsing boundaries;
- silent fallback from missing provider to default native/mock/HTTP behavior;
- provider access to host EF entities or tracked DbContext instances;
- tests that seed DTOs but do not exercise the production handler, worker, store, or driver boundary being claimed.
