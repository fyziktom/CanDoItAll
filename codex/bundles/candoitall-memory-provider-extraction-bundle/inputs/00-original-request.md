# Original Request Preservation

## Current bundle request

The user asked for a deep review of the previous architecture package and then for a concrete English implementation bundle with many detailed subbundles, phased sequencing, and checkpoint/refactoring subbundles after phases. The bundle must follow the established CanDoItAll bundle convention under `codex/skills/bundles`, must be grounded in the current repository and the previous architecture ZIP, and must not simplify or skip the original requirements.

## Original architecture request reconstructed for execution

The original architecture objective was to separate the Cognitive Memory core from CanDoItAll into a standalone service while creating a generic module for connecting different memories, not only the native CanDoItAll memory. The motivation is adoption, flexibility, independent evolution of the native engine, parallel operation of multiple memories, and removing Qdrant as a base dependency so a new user can start with only PostgreSQL and the app.

The main CanDoItAll memory module must become a generic UI and interfacing wrapper for memory providers. It must support common surfaces such as browsing records, chatting/querying memory, provider configuration, operations, and feedback. Native Cognitive Memory may expose richer surfaces such as professor probing, clusters, review queue, quality operations, and self-regulation through Blazor RCL components or iframe/external UI projection.

The module must define interfaces and communication patterns. The simplest provider accepts a query and returns context. More advanced memories can be eventful and proactive: they can emit hypotheses, ask agents to verify facts, request source data, push feedback requests, or start long-running operations. The architecture must therefore support both synchronous query-response and asynchronous event-driven flows.

Agents and workflows need generic memory tools and workflow executors. Tool and executor logic must share the same operation core. Each agent, process, or workflow node must be able to select a concrete memory provider, for example a programming memory for developer agents and a business-analysis memory for business agents. MAF and the generic memory tool must not depend directly on native Cognitive Memory.

The native service target repository is `C:\repositories\CanDoItAll.CognitiveMemory`. It should own engine code and the native service wrapper. It may depend on MAF abstractions/runtimes for curator or professor agents, but it must not depend on the main CanDoItAll Agent module. MAF and the main application must not take a direct dependency on native Cognitive Memory.

The native memory needs its own database, EF model, migrations, testable `IDbContextFactory` setup, InMemory support, correct async usage, and no mixing of native memory records into the main CanDoItAll database. The generic CanDoItAll memory module may persist integration metadata such as provider profiles, operation ledger, event inbox/outbox, feedback correlations, and source request state.

Ingestion must support source data from CanDoItAll modules such as project structures, processes, CRM, resources, and other future modules. Providers may request source snapshots through a structured Source Gateway, or users may explicitly click actions such as `Ingest into memory` on a project or completed process. Source access must be policy-governed, snapshot-based, and must not expose the host `AppDbContext` directly to providers.

The memory protocol must be more structured than plain text. Requests should include workspace, project, process step, tags, budgets, requester identity, agent identity, provenance, sensitivity, policy, and arbitrary structured facts. Responses may include context packs, citations/source refs, confidence, warnings, operation ids, provider events, and feedback handles.

The current Cognitive Memory is deeply introduced into MAF and must be uncoupled. It should become a provider behind generic tool/executor/context contributor abstractions rather than a hard agent step.

Memory requests may run across the network and may take minutes. The architecture must support timeout policy, cancellation, long-running operation status, polling, callbacks, batching, balancing, and shared primitives potentially reused with LLM provider operations.

The generic module must track which agent/process/session requested context, which provider produced which context pack, and what feedback later arrived. Feedback can be immediate, process-completion-based, or delayed until real economic impact is known. The ledger should support TTL/retention, forget policies, and optional IPFS snapshots that are unpinned when forgotten.

The native Cognitive Memory is not complete. Future features include richer feedback, resource monitoring, runtime/event pushing, governance/economic control, and broader self-regulation. The implementation plan must leave extension seams and must not freeze the native service to the current implementation.
