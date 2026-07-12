# Cognitive Memory And Generic Memory Providers

This section documents the current post-extraction memory architecture. The base CanDoItAll app now owns a generic Memory Provider runtime. Native Cognitive Memory is an optional provider/service path, not a base startup dependency.

## Current Stage

The memory provider extraction is in its final SB40 architecture and validation gate as of 2026-07-12:

- Base startup supports zero configured memory providers.
- Generic memory contracts, provider registry, ledgers, workers, Source Gateway, Agent Framework integration, and `/memory` UI are implemented in the main repo.
- HTTP, native-remote, and MCP provider drivers are opt-in through configuration.
- Agents own ordered provider bindings and choose `Disabled`, `Automatic`, or explicit `/mem:<alias>` invocation.
- The deterministic mock driver is explicit test/development configuration only.
- The native `CanDoItAll.CognitiveMemory` service lives in `C:\repositories\CanDoItAll.CognitiveMemory` and is validated separately.
- Legacy main database `CognitiveMemory_*` tables are retained read-only with an export path.
- The old main-repo Cognitive Memory module remains only as retained legacy/native regression coverage until a follow-up native-suite migration deletes or moves it.

## Start Here

- [Stage assessment](current-state/stage-assessment.md): release status, boundaries, residual risks, and validation evidence.
- [Implementation map](current-state/implementation-map.md): source folders, registration, services, UI, native service, and legacy-retained areas.
- [Provider setup](operations/provider-setup.md): zero-provider, mock, HTTP, MCP, and native-remote setup.
- [Agent memory configuration](operations/agent-memory.md): ordered provider bindings, automatic mode, explicit `/mem:<alias>` routing, and failure behavior.
- [Provider authoring](operations/provider-authoring.md): contracts and rules for adding a memory provider.
- [Release notes](operations/release-notes-memory-provider-extraction.md): operator-visible behavior changes, migration, rollback, and readiness decision.
- [Legacy main DB retirement](operations/legacy-main-db-retirement.md): read-only export and retirement path for historical main database `CognitiveMemory_*` tables.
- [Memory test suite rebalance](operations/memory-test-suite-rebalance.md): generic provider tests, explicit mock fixtures, and retained legacy native test ownership.
- [Validation and testing](operations/validation-and-testing.md): targeted commands and final release proof.

Historical P0/P1 native Cognitive Memory documents remain in this folder for context. Treat them as native-provider history unless the page explicitly says it describes the current generic provider runtime.

## Primary Source References

- `src/Memory/CanDoItAll.Memory.Abstractions`
- `src/Memory/CanDoItAll.Memory.Application`
- `src/Memory/CanDoItAll.Memory.Persistence`
- `src/Memory/CanDoItAll.Memory.Http`
- `src/Memory/CanDoItAll.Memory.Mcp`
- `src/Modules/CanDoItAll.Modules.Memory`
- `src/MAF/Memory/CanDoItAll.AgentFramework.Memory`
- `src/Memory/CanDoItAll.Memory.SourceGateway.Abstractions`
- `C:\repositories\CanDoItAll.CognitiveMemory`

Retained legacy-native references:

- `src/Modules/CanDoItAll.Modules.CognitiveMemory`
- `tests/*/*CognitiveMemory*.cs`
- `tests/Support/CanDoItAll.Tests.Support/CognitiveMemory`

These retained references must not be used as proof that the base host still depends on native Cognitive Memory. Base-host decoupling is guarded by generic memory tests and source audits.

## Architecture Summary

1. The base app registers generic memory persistence, runtime services, `/memory` UI, Source Gateway adapters, and MAF memory integration.
2. With no provider configured, runtime calls return typed no-provider diagnostics and do not dispatch to native, Qdrant, OpenAI, or mock providers.
3. Provider profiles select driver kinds such as HTTP, MCP, native remote, or explicit mock. Profile extensions carry transport-specific settings.
4. Agent context calls route through the dedicated Agent Framework Memory adapter, then the shared generic operation handler and ledgers. Only complete operations are exposed to agents.
5. Source data reaches providers through Source Gateway snapshots, not host EF entities.
6. Operation, feedback, provider event, event outbox, source request, retention, and health state have generic ledger contracts. Shipping drivers advertise only the subset they implement end to end.
7. Native Cognitive Memory is exposed as an optional remote provider through the generic protocol. Its DB, engine, workers, and access policy are owned by its external repository; generic agent behavior remains in the main app.
8. Historical main database native memory tables are not dropped by the main app. Operators can export them and retain them read-only.

## Release Boundary

The base host may ship without native Cognitive Memory, Qdrant, SemanticCompletion, or any memory provider enabled. Enabling a provider is an explicit operator/configuration action. The release gate is documented in [release notes](operations/release-notes-memory-provider-extraction.md).
