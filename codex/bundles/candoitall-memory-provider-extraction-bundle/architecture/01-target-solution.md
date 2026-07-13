# Target Solution

## Target architecture summary

The target architecture has three layers:

1. **Generic Memory Provider Module in CanDoItAll**: owns provider registry, provider profiles, generic protocol contracts, driver factories, operation ledger, feedback ledger, event inbox/outbox, source gateway, common UI, and MAF integration.
2. **Provider Implementations**: HTTP driver, MCP-style driver, mock driver, and native Cognitive Memory remote driver implement the same provider protocol.
3. **Native CanDoItAll.CognitiveMemory Service**: owns native memory domain, DB, migrations, recall/consolidation/quality/probing/review/self-regulation, optional Qdrant projection, workers, and advanced UI surfaces.

The 2026-07-05 re-entry adds one implementation constraint: the target architecture must be fitted into the current MAF refactor. Memory tools, workflow executors, context contribution, provider selection, and source snapshots must integrate through the current MAF contracts listed in `analysis/04-live-repo-reentry-alignment.md`.

## Strangler migration path

The implementation must not start by moving the current module wholesale. Instead:

1. Introduce generic contracts and runtime.
2. Wrap the current in-process Cognitive Memory implementation as a temporary generic provider adapter.
3. Replace MAF and workflow integration with generic memory tools/executors/context contributor.
4. Create the native service repository/projects and migrate persistence/domain/engine code.
5. Expose native service through the protocol and switch the main host from in-process adapter to remote/native driver.
6. Remove old direct project references, Qdrant base startup configuration, AppDbContext native memory model registrations, and native API endpoints from the main host.

The strangler bridge must not become a fallback provider. It may be used only when an explicit temporary native in-process provider profile is configured during migration.

## Target subproject shape in main CanDoItAll

- `CanDoItAll.Memory.Abstractions`: protocol DTOs, provider interfaces, source snapshot contracts, event/feedback/operation contracts.
- `CanDoItAll.Memory.Application`: provider registry, operation handler, source gateway orchestration, feedback service, event router, policy evaluation, driver factory composition.
- `CanDoItAll.Memory.Persistence`: generic integration metadata EF records/configurations for provider profiles, operations, feedback, event inbox/outbox, and source request ledger.
- `CanDoItAll.Memory.Drivers.Http`: simple HTTP provider driver with timeout/auth/resilience profile support.
- `CanDoItAll.Memory.Drivers.Mcp`: MCP-style adapter/driver and capability negotiation support.
- `CanDoItAll.Memory.Drivers.Mock`: deterministic in-process mock providers for tests and demos.
- `CanDoItAll.Memory.UI`: Blazor/RCL components for provider management, query/chat, operations, feedback, and event inbox.
- `CanDoItAll.Modules.Memory`: app module wrapper, endpoints, navigation, UI composition, and source adapter registrations.
- `CanDoItAll.AgentFramework.Memory`: MAF tool/executor/context contributor integration that depends only on generic memory contracts/application abstractions and current MAF abstractions. It should expose `IAgentRuntimeToolProvider`, `IWorkflowExecutor` or `IWorkflowExecutorDescriptorSource` where appropriate, and `IAgentContextContributor`; it must not introduce a parallel MAF runtime.

## Target subproject shape in CanDoItAll.CognitiveMemory

- `CanDoItAll.CognitiveMemory.Contracts`: native public contracts, protocol mappings, and optional advanced native DTOs.
- `CanDoItAll.CognitiveMemory.Domain`: native records/value objects/services that do not depend on host AppDbContext.
- `CanDoItAll.CognitiveMemory.Persistence`: native DbContext, EF configurations, migrations, InMemory/PostgreSQL profiles.
- `CanDoItAll.CognitiveMemory.Application`: recall, ingestion, consolidation, feedback, review, quality, self-regulation, scoring, and orchestration services.
- `CanDoItAll.CognitiveMemory.Projection.Rag`: optional semantic/RAG/Qdrant projection adapter.
- `CanDoItAll.CognitiveMemory.Maf`: curator/professor agent integration through MAF abstractions only.
- `CanDoItAll.CognitiveMemory.Service`: HTTP API implementing Memory Protocol v1 and native advanced endpoints.
- `CanDoItAll.CognitiveMemory.Workers`: scheduled automation, operation processing, event pushing, cleanup, and retention.
- `CanDoItAll.CognitiveMemory.UI`: provider-specific rich UI RCL or standalone web UI projected into the main host.

## Closure definition

The migration is complete only when the main CanDoItAll application starts without native Cognitive Memory and Qdrant, generic memory works with mock/simple providers, native Cognitive Memory works as an optional provider, MAF uses only generic memory contracts, and source/feedback/event flows are proven through tests and observable operations.

Zero configured memory providers is a supported target state. In that state generic memory services, provider management UI, architecture guards, and MAF registration must remain functional while memory operations return typed no-provider diagnostics or skip by policy.
