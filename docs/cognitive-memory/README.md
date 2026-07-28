# Generic Memory And Retired Cognitive Memory

The active host uses the provider-neutral Memory runtime. The old native Cognitive Memory module is not registered by the base composition root and its HTTP surface is retired.

## Current Boundary

- `/memory` is the operator UI for provider profiles, operations, and ledgers.
- Agent memory settings and operations are part of `/api/agents`; the base host does not expose a general `/api/memory` route family.
- `GET /api/cognitive-memory/contract` and `GET /api/cognitive-memory/v1/contract` report the retirement contract. Every other request below either base path returns `410 Gone`.
- Mock, HTTP, native-remote, and MCP drivers are opt-in. All drivers and memory background workers are disabled in the tracked default configuration.
- A zero-provider deployment is supported. Missing capabilities fail with typed diagnostics; the runtime does not silently fall back to another provider.
- Qdrant, SemanticCompletion, OpenAI, and the retained legacy module are not base-host memory dependencies.

The former native implementation is retained under `src/Modules/CanDoItAll.Modules.CognitiveMemory` only for legacy compatibility and regression coverage. It is excluded from `CanDoItAll.slnx` and active module discovery. New work must target the generic Memory projects or the separately owned native service.

## Maintained Guidance

- [Implementation map](current-state/implementation-map.md)
- [Provider setup](operations/provider-setup.md)
- [Agent memory configuration](operations/agent-memory.md)
- [Provider authoring](operations/provider-authoring.md)
- [Legacy main database retirement](operations/legacy-main-db-retirement.md)
- [Validation and testing](operations/validation-and-testing.md)

Removed native API, Qdrant, roadmap, and beta-proof pages remain available in Git history. They are not a contract for the current host.

## Source Of Truth

| Area | Source |
| --- | --- |
| Provider contracts | `src/Memory/CanDoItAll.Memory.Abstractions` |
| Runtime and dispatch | `src/Memory/CanDoItAll.Memory.Application` |
| Persistence and workers | `src/Memory/CanDoItAll.Memory.Persistence` |
| HTTP and native-remote drivers | `src/Memory/CanDoItAll.Memory.Http` |
| MCP driver | `src/Memory/CanDoItAll.Memory.Mcp` |
| Explicit mock driver | `src/Memory/CanDoItAll.Memory.Mock` |
| Source Gateway contracts | `src/Memory/CanDoItAll.Memory.SourceGateway.Abstractions` |
| UI module | `src/Modules/CanDoItAll.Modules.Memory` |
| Agent integration | `src/MAF/Memory/CanDoItAll.AgentFramework.Memory` |
| Retirement HTTP shim | `src/App/CanDoItAll.Web/Api/CognitiveMemoryApi.cs` |
