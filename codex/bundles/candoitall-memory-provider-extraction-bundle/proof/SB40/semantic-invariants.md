# SB40 Semantic Invariants

## SB40-INV-01 - Agent Choice Controls Every Query

- Invariant ID: SB40-INV-01
- Source raw note: one agent may use multiple configured memory providers, with settings choosing automatic use or an explicit mem:<alias> directive.
- Expected behavior: Disabled and explicit-without-directive paths dispatch zero providers; automatic mode dispatches only configured bindings in stable order; an explicit authorized alias dispatches only that provider and is removed from provider/model text.
- Disallowed shallow implementation: UI-only fields, registry-order fallback, querying every registered provider, or parsing directive-looking quoted/code text.
- Failing-first test: bundle://proof/SB36/transcripts/failing-first-evidence.txt records the non-zero implicit-fallback characterization.
- Passing test: bundle://proof/SB40/transcripts/terminal-validation.txt records the real contributor-handler-driver-ledger seam and the 196-pass aggregate.
- Changed source files: repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Routing/MemoryDirectiveParser.cs, repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Routing/AgentMemoryInvocationPlanner.cs, and repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Context/MemoryAgentContextFanOut.cs.
- Production assertions: the runtime plan is typed, bounded, authorized, stable-order, and its provider content is framed as untrusted data.
- Red-team negative case: unknown, duplicate, quoted, code-literal, and unbound aliases must produce typed rejection and zero dispatch; bundle://proof/SB40/transcripts/red-team-closure.txt audits the fake-proof trap.
- Downstream dependency check: the agents and memory routes were exercised through production composition at desktop and narrow viewports in bundle://proof/SB40/transcripts/browser-validation.txt.

## SB40-INV-02 - External And Transport Boundaries Fail Closed

- Invariant ID: SB40-INV-02
- Source raw note: any external memory provider must be connectable without trusting its content, credentials, project identity, or response size.
- Expected behavior: environment references supply credentials, request identity remains typed, external project authorization precedes recall, response caps apply before materialization, and provider feedback correlation cannot control host operations.
- Disallowed shallow implementation: raw credential persistence, caller-controlled global fallback, post-mapping size checks, provider-supplied feedback handles, or in-process-only external proof.
- Failing-first test: failing-first N/A for the process reconstruction because no full pre-repair external binary/transcript was preserved; no production failure is fabricated. SB35 inventory and SB39 before/after hashes preserve the baseline.
- Passing test: bundle://proof/SB40/transcripts/terminal-validation.txt records external Release build/tests and live main-driver process conformance; bundle://proof/SB40/transcripts/source-and-architecture-audit.txt records response-cap and feedback-handle negatives.
- Changed source files: repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderResponseReader.cs, repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderResponseGuard.cs, and repo://src/Memory/CanDoItAll.Memory.Application/MemoryContextPackFeedbackPolicy.cs.
- Production assertions: HTTP, NativeRemote, and MCP reject oversized or malformed provider output before mapping; external access policy and project authorization prevent forbidden content from leaving the external application boundary.
- Red-team negative case: wrong credential/project, malformed response, chunked oversize, MCP oversize, and hostile feedback handle are rejected in the passing suites.
- Downstream dependency check: the real main NativeRemote driver queried the separately launched service and completed the generic ledger lifecycle.

## SB40-INV-03 - Modular Ownership And Worker Coordination Are Real

- Invariant ID: SB40-INV-03
- Source raw note: remove capability-grouping partial classes and move responsibilities to proper projects, folders, namespaces, and independently testable collaborators.
- Expected behavior: Memory Application remains agent-agnostic, AgentFramework.Memory owns agent routing, composition explicitly registers drivers, the retired catalog-memory/Mem0 path cannot attach a parallel provider, no relevant handwritten partial or oversized target remains, and PostgreSQL workers acquire fenced distributed phase leases.
- Disallowed shallow implementation: renamed partial buckets, a new god facade, service-locator composition, duplicate driver kinds, a legacy Mem0 catalog/template/runtime attachment beside generic bindings, or process-local locks presented as multi-replica safety.
- Failing-first test: bundle://proof/SB36/transcripts/failing-first-evidence.txt and the SB35 architecture inventory preserve the shallow baseline.
- Passing test: bundle://proof/SB40/transcripts/source-and-architecture-audit.txt records 227 scoped files with zero partials/oversized files, 88 projects with zero project cycles, zero retired Mem0 tokens in production src, explicit unique drivers, and lease disposition. The exact retirement Integration tests passed 2/2 and focused runtime/template/cleanup Unit tests passed 51/51 in bundle://proof/SB40/transcripts/terminal-validation.txt.
- Changed source files: repo://src/App/CanDoItAll.Composition/Memory/MemoryRuntimeServiceCollectionExtensions.cs, repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Capabilities/LegacyMemoryCapabilityPolicy.cs, repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs, repo://src/Memory/CanDoItAll.Memory.Persistence/PostgreSqlMemoryWorkerLeasePersistence.cs, and repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260712133000_DistributedMemoryWorkerPhaseLeases.cs.
- Production assertions: retired catalog-memory capabilities are rejected or filtered instead of attaching Mem0; owner/token fencing, renewal, expiry, and default-disabled workers are explicit; InMemory coordination is documented as process-local.
- Red-team negative case: legacy Mem0 attachment/template/package/configuration tokens, duplicate registration, lease theft/expiry, foreign operation owner, prohibited partial, and stub scans are part of the terminal suite/audit. Negative guard token literals intentionally remain in tests and are not misreported as production matches.
- Downstream dependency check: final main build, Memory/MAF/focused Unit/Components/exact Integration/Playwright suites, and external independent build/tests passed. A separate broad seed-instruction class has 4 pre-existing stale assertions and is explicitly not claimed green.

## SB40-INV-04 - Closure Uses Real Browser And Process Evidence

- Invariant ID: SB40-INV-04
- Source raw note: analyze whether agents can really use one or many providers, repair the implementation, and test it.
- Expected behavior: real hosted UI persists two aliases and explicit settings at desktop/narrow widths; a real runtime seam produces provider-labelled context and ledgers; a real external process interoperates through the main driver.
- Disallowed shallow implementation: screenshots without actions, DTO-only packs, mocks used as external proof, or historical focused counts treated as the final gate.
- Failing-first test: bundle://proof/SB36/transcripts/failing-first-evidence.txt records the preserved non-zero selection/ownership baseline.
- Passing test: bundle://proof/SB40/transcripts/browser-validation.txt and bundle://proof/SB40/transcripts/terminal-validation.txt record 5/5 browser tests, the real agent seam, live external conformance, and final builds/suites.
- Changed source files: repo://tests/Memory/CanDoItAll.Memory.Tests/AgentMemoryProviderEndToEndTests.cs and repo://tests/Playwright/CanDoItAll.Tests.Playwright/AgentMemoryProviderSettingsPlaywrightTests.cs.
- Production assertions: browser state, provider query, binding decision, dispatch, ledger, returned context, and sanitized prompt are correlated across production owners rather than manually constructing the final pack.
- Red-team negative case: bundle://proof/SB40/transcripts/red-team-closure.txt rejects UI-only, registry-only, in-process-only, and manually seeded proof.
- Downstream dependency check: no downstream subbundle remains; merge/release relies on the completed validator and the residual-risk register.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Agent provider invocation plan | repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Routing/AgentMemoryInvocationPlanner.cs | repo://src/MAF/Memory/CanDoItAll.AgentFramework.Memory/Context/MemoryAgentContextFanOut.cs | bundle://proof/SB40/transcripts/terminal-validation.txt | unknown/duplicate/no-directive zero-dispatch tests in bundle://proof/SB40/transcripts/terminal-validation.txt |
| Persisted operation ownership | repo://src/Memory/CanDoItAll.Memory.Application/MemoryQueryOperationService.cs | repo://src/Memory/CanDoItAll.Memory.Application/MemoryOperationAccessAuthorizer.cs | bundle://proof/SB40/transcripts/terminal-validation.txt | foreign-owner characterization and passing negatives in bundle://proof/SB36/transcripts/failing-first-evidence.txt |
| Distributed worker phase lease | repo://src/Memory/CanDoItAll.Memory.Persistence/PostgreSqlMemoryWorkerLeasePersistence.cs | repo://src/Memory/CanDoItAll.Memory.Persistence/Hosting/MemoryWorkerLeaseRunner.cs | repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260712133000_DistributedMemoryWorkerPhaseLeases.cs | lease theft/expiry tests recorded by bundle://proof/SB40/transcripts/source-and-architecture-audit.txt |
| External authenticated recall | bundle://proof/SB39/transcripts/file-hashes.txt | repo://src/Memory/CanDoItAll.Memory.Http/NativeRemoteMemoryProviderDriver.cs | bundle://proof/SB40/transcripts/terminal-validation.txt | wrong credential/project and access-policy negatives in bundle://proof/SB39/transcripts/external-local-and-isolated-validation.txt |
| Legacy catalog-memory retirement | repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Capabilities/LegacyMemoryCapabilityPolicy.cs | repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs | bundle://proof/SB40/transcripts/terminal-validation.txt | production zero-token scan plus retired capability rejection/filter tests in bundle://proof/SB40/transcripts/source-and-architecture-audit.txt |
