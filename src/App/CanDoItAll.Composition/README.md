# CanDoItAll.Composition

## Purpose

Composition root for runtime modules, shared services, infrastructure, provider setup, and component assembly discovery.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/App/CanDoItAll.Composition/CanDoItAll.Composition.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Composition.csproj](CanDoItAll.Composition.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Composition is the boundary where modules, infrastructure, provider configuration, and shared components are wired together. Keep registrations explicit and avoid moving domain behavior into startup code.

### Generic memory background workers

Durable memory workers are disabled unless the host explicitly opts in:

```json
{
  "Memory": {
    "BackgroundWorkers": {
      "Enabled": true,
      "CycleInterval": "00:00:05",
      "LeaseDuration": "00:01:00",
      "LeaseRenewalInterval": "00:00:20"
    }
  }
}
```

Each phase acquires a database-backed lease before it polls asynchronous operations, delivers feedback, polls or drains provider events, or applies retention. Leases are renewed while work runs and completion is accepted only from the current owner and token. Expired leases are recoverable by another replica. This deliberately serializes each phase across replicas; it does not distribute one phase's batch across hosts.

Production multi-replica hosting requires every replica to use the same PostgreSQL database and to apply the migration that creates `Memory_WorkerLeases`. The InMemory provider coordinates only inside one process and is not a distributed deployment option. Manual source-capture records remain queued-only and have no delivery worker; adding one requires a dedicated leased phase before it can be enabled.

### External memory drivers

External transports are explicit host capabilities. Enable only the transports this deployment is allowed to call:

```json
{
  "Memory": {
    "Providers": {
      "Http": { "Enabled": true },
      "NativeRemote": { "Enabled": true },
      "Mcp": { "Enabled": true }
    }
  }
}
```

Provider profiles are managed separately in the Memory UI. Profiles store endpoint and environment-variable references, never credential values. HTTP/native credential variables contain the secret token; the configured authorization scheme is applied by the driver. An MCP header-binding variable contains the complete header value, for example `Bearer <token>`.

The generic HTTP transport and the Cognitive Memory adapter are separate assemblies. `CanDoItAll.Memory.Drivers.CognitiveMemory` owns the provider-specific profile keys and maps them onto the generic HTTP protocol client without referencing the standalone Cognitive Memory repository.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
