# CanDoItAll.AgentFramework.ProviderPipelines

## Purpose

Provider pipeline primitives for local batching and queueing of provider requests.

This project owns the reusable batching contracts and local dispatcher used by provider runtime code. It has no dependency on concrete provider drivers.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.AgentFramework.ProviderPipelines/CanDoItAll.AgentFramework.ProviderPipelines.csproj
```

## References

Project references:

- `../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`

Framework references:

- None

Direct package references:

- None

## Runtime Responsibilities

- `ProviderBatchPolicy` combines a dispatch key, dispatch limits, and queue-full behavior.
- `ProviderBatchEnvelope<TPayload>` carries one provider request into a batching lane with an optional correlation id.
- `ProviderLocalBatchDispatcherHub<TPayload, TResult>` owns per-key dispatcher instances and bypasses batching when limits do not support batching.
- `ProviderLocalBatchDispatcher<TPayload, TResult>` queues requests, builds bounded batches, enforces max in-flight batches, and completes each caller with the matching per-item result.

## Configuration

Batching behavior is driven by `ProviderDispatchLimits`. Batching requires a max batch size greater than one, a positive in-flight batch count, queue depth at least as large as the batch size, non-negative queue delay, and a positive request timeout.

Queue-full behavior is explicit. `FailFast` returns `ProviderBatchQueueCapacityExceededException`; other behavior waits for queue capacity. Do not silently drop queued provider work.

## Architecture Notes

Keep this project provider-neutral. It should not reference MAF, UI modules, process modules, or concrete provider SDKs. Provider-specific batch execution belongs in the caller-provided delegate passed to the dispatcher.

The dispatcher is responsible for correlation, queueing, batching, cancellation, and result fan-out. It should not interpret provider payload content.

## Validation

Useful focused validation commands:

```powershell
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ProviderLocalBatchDispatcher"
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ProviderBatchJobBalancer"
```

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
- Provider runtime README: `src/CanDoItAll.AgentFramework.Providers/README.md`
