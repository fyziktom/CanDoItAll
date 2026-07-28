# Memory Providers

The provider-neutral Memory runtime and its provider integrations are experimental
work in progress. Native Cognitive Memory is a separate, unpublished work-in-progress
service owned by the standalone
[CanDoItAll.CognitiveMemory repository](https://github.com/fyziktom/CanDoItAll.CognitiveMemory).
It can connect to this host only through an explicitly enabled external provider.

## Current Boundary

- `/memory` is the operator UI for provider profiles, operations, and ledgers.
- Agent memory settings and operations are part of `/api/agents`.
- Provider profiles and supported provider operations are exposed through the experimental `/api/memory-providers` API.
- The base host does not expose `/api/cognitive-memory`; native service APIs belong to the standalone repository.
- Mock, HTTP, native-remote, and MCP drivers are opt-in. All drivers and memory background workers are disabled in the tracked default configuration.
- A zero-provider deployment is supported. Missing capabilities fail with typed diagnostics; the runtime does not silently fall back to another provider.
- Qdrant, SemanticCompletion, OpenAI, and the native service implementation are not base-host memory dependencies.

This repository does not reference native Cognitive Memory source or packages. Its isolated `src/Memory/Drivers/CanDoItAll.Memory.Drivers.CognitiveMemory` project translates the generic provider protocol to the external service. Native domain, persistence, runtime, UI, and tests belong in the standalone repository.

## Maintained Guidance

- [Implementation map](current-state/implementation-map.md)
- [Provider setup](operations/provider-setup.md)
- [Agent memory configuration](operations/agent-memory.md)
- [Provider authoring](operations/provider-authoring.md)
- [Legacy main database retirement](operations/legacy-main-db-retirement.md)
- [Validation and testing](operations/validation-and-testing.md)

Removed native implementation, API, Qdrant, roadmap, and beta-proof pages remain
available in Git history. They are not a contract for the current host.

## Source Of Truth

| Area | Source |
| --- | --- |
| Provider contracts | `src/Memory/CanDoItAll.Memory.Abstractions` |
| Runtime and dispatch | `src/Memory/CanDoItAll.Memory.Application` |
| Persistence and workers | `src/Memory/CanDoItAll.Memory.Persistence` |
| Generic HTTP driver | `src/Memory/CanDoItAll.Memory.Http` |
| Cognitive Memory external-service driver | `src/Memory/Drivers/CanDoItAll.Memory.Drivers.CognitiveMemory` |
| MCP driver | `src/Memory/CanDoItAll.Memory.Mcp` |
| Explicit mock driver | `src/Memory/CanDoItAll.Memory.Mock` |
| Source Gateway contracts | `src/Memory/CanDoItAll.Memory.SourceGateway.Abstractions` |
| UI module | `src/Modules/CanDoItAll.Modules.Memory` |
| Agent integration | `src/MAF/Memory/CanDoItAll.AgentFramework.Memory` |
| Provider HTTP API | `src/App/CanDoItAll.Web/Api/MemoryProvidersApi.cs` |
