# Memory Providers

CanDoItAll exposes a provider-neutral Memory subsystem for operations, source ingestion,
retention, ledgers, feedback, and agent context. Providers and background workers are
disabled until explicitly configured.

## Runtime Boundary

- `/memory` provides provider profiles, operation monitoring, and ledgers.
- Agent memory settings and operations are exposed through `/api/agents`.
- Provider profiles and operations are exposed through `/api/memory-providers`.
- A deployment with no enabled provider is valid.
- Missing provider capabilities return typed diagnostics.
- HTTP, MCP, mock, and external Cognitive Memory drivers are explicit adapters.

Native Cognitive Memory implementation is owned by
[CanDoItAll.CognitiveMemory](https://github.com/fyziktom/CanDoItAll.CognitiveMemory).
This repository contains only the external-service adapter.

## Guidance

- [Provider setup](operations/provider-setup.md)
- [Agent memory configuration](operations/agent-memory.md)
- [Provider authoring](operations/provider-authoring.md)
- [Validation and testing](operations/validation-and-testing.md)

## Source Map

| Area | Project |
|---|---|
| Provider contracts | [`CanDoItAll.Memory.Abstractions`](../../src/Memory/CanDoItAll.Memory.Abstractions/README.md) |
| Operations and dispatch | [`CanDoItAll.Memory.Application`](../../src/Memory/CanDoItAll.Memory.Application/README.md) |
| Persistence and workers | [`CanDoItAll.Memory.Persistence`](../../src/Memory/CanDoItAll.Memory.Persistence/README.md) |
| HTTP provider | [`CanDoItAll.Memory.Http`](../../src/Memory/CanDoItAll.Memory.Http/README.md) |
| MCP provider | [`CanDoItAll.Memory.Mcp`](../../src/Memory/CanDoItAll.Memory.Mcp/README.md) |
| Deterministic mock | [`CanDoItAll.Memory.Mock`](../../src/Memory/CanDoItAll.Memory.Mock/README.md) |
| Source gateway contracts | [`CanDoItAll.Memory.SourceGateway.Abstractions`](../../src/Memory/CanDoItAll.Memory.SourceGateway.Abstractions/README.md) |
| External Cognitive Memory adapter | [`CanDoItAll.Memory.Drivers.CognitiveMemory`](../../src/Memory/Drivers/CanDoItAll.Memory.Drivers.CognitiveMemory/README.md) |
| Product module | [`CanDoItAll.Modules.Memory`](../../src/Modules/CanDoItAll.Modules.Memory/README.md) |
| Agent integration | [`CanDoItAll.AgentFramework.Memory`](../../src/MAF/Memory/CanDoItAll.AgentFramework.Memory/README.md) |
