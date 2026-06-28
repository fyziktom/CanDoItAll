# CanDoItAll.AgentFramework.Providers

## Purpose

Provider driver contracts and runtime infrastructure for provider-backed AgentFramework operations.

This project owns concrete provider drivers, provider capability contracts, provider runtime descriptors, pooled runtime handles, dispatch lane gates, and batch job balancing. MAF, voice, image generation, and module-level provider services should use this layer instead of calling provider SDKs directly from UI or process code.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.AgentFramework.Providers/CanDoItAll.AgentFramework.Providers.csproj
```

## References

Project references:

- `../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `../CanDoItAll.AgentFramework.ProviderPipelines/CanDoItAll.AgentFramework.ProviderPipelines.csproj`

Framework references:

- None

Direct package references:

- None

## Runtime Responsibilities

- `IAgentProviderDriver` and capability-specific driver interfaces define provider operations with typed request and result models.
- Concrete driver registration wires OpenAI, Azure OpenAI, Ollama, and ComfyUI drivers into `AgentProviderDriverRegistry`.
- `ProviderRuntimeDescriptorStore`, `ProviderRuntimePool`, and `ProviderRuntimeHandle` maintain provider-scoped runtime handles and replace stale handles when descriptors change.
- `ProviderDispatchLaneGate` enforces provider/model/operation concurrency from `ProviderDispatchLimits`.
- `ProviderBatchJobBalancer` plans and executes provider-backed batch work while respecting provider selection, dispatch limits, retries, and checkpointed recovery.

## Configuration

Provider drivers read provider metadata from `ProviderProfile`, including kind, base URL, credential name, model, transport, purpose, and provider-specific JSON. Credential resolution must fail explicitly when a required secret is missing. Do not add silent fallback credentials or inferred provider behavior.

`ProviderDispatchLimits.Unbatched` defaults to one in-flight request. Tests or drivers that need higher unbatched concurrency must request it explicitly through `maxInFlightRequests`.

## Architecture Notes

Keep provider-specific protocol details in driver classes. Keep dispatch, pooling, and batching behavior in runtime and batching types. UI, process, and MAF callers should depend on typed provider services or runtime gateways, not on concrete driver classes.

Unsupported provider capabilities should throw `UnsupportedProviderCapabilityException` with the provider kind and capability. Runtime failures should preserve actionable provider state while redacting credentials.

## Validation

Useful focused validation commands:

```powershell
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~Provider"
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ProviderRuntime|FullyQualifiedName~ProviderDispatchLaneGate|FullyQualifiedName~ProviderBatchJobBalancer"
```

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
- Process/MAF/provider implementation map: `docs/processes-maf-providers-implementation-map.md`
