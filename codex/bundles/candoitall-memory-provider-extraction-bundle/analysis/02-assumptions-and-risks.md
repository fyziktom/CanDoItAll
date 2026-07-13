# Assumptions And Risks

## Working Assumptions

- The live `C:\repositories\CanDoItAll` repository is the current implementation state for this re-entry refresh.
- The separate target repository `C:\repositories\CanDoItAll.CognitiveMemory` exists and is clean, but currently contains only `README.md`; SB24 must scaffold or align the first native solution/projects.
- The main CanDoItAll app may continue using its existing AppDbContext for generic integration metadata such as provider profiles, ledgers, and event inbox/outbox; native memory domain records must move out.
- Native Cognitive Memory can temporarily be wrapped in-process behind a generic provider adapter before the final remote service driver is enabled.
- Existing MAF capability/tool/executor/context-contribution/source-snapshot work must be reused or deliberately rehomed for memory tool registration, provider selection, and source gateway behavior. Native memory logic must not be copied into MAF.
- Qdrant and SemanticCompletion remain valid optional native provider dependencies, but not mandatory base runtime dependencies.

## Critical Path Risks

- The risks below are blocking for the critical path; any unresolved high-severity item must be mitigated by the owning checkpoint subbundle before downstream execution continues.

| Risk | Severity | Mitigation in bundle |
| --- | --- | --- |
| Generic protocol is too text-only and cannot support future eventful memory. | High | SB01-SB04 define structured envelopes, capability manifests, events, feedback handles, source snapshots, and extension payloads before runtime work. |
| Direct MAF/native memory coupling survives under renamed classes. | High | SB15-SB19 require generic operation handler, provider selection, dependency guards, and hard-link removal before native service extraction. |
| Native memory extraction becomes a big-bang move and breaks tests. | High | SB24-SB29 use scaffold, own DB, domain migration, protocol API, and hardening gates before dependency removal. |
| Source ingestion leaks host `AppDbContext` or EF entities into providers. | High | SB04 and SB11-SB14 isolate source gateway contracts, adapters, redaction, provenance, and checkpoint audits. |
| New Source Gateway contracts duplicate existing MAF `MemorySourceSnapshot*` contracts and split source truth. | High | SB04 must rehome, wrap, or explicitly migrate the existing source snapshot contracts from MAF instead of creating a parallel incompatible model. |
| Feedback correlation is added too late and cannot connect context packs to outcomes. | High | SB03 introduces feedback ledger and context delivery correlation in the foundation phase. |
| Long-running memory operations block agents or HTTP requests. | Medium | SB03, SB07, SB09, and SB10 require async operation model, timeout policy, cancellation, workers, and event/polling status. |
| UI becomes native-memory-only again. | Medium | SB20-SB23 split common UI shell from provider-specific RCL/iframe surfaces and require zero-provider and mock-provider proof. |
| No-provider startup path accidentally falls back to native memory, OpenAI, or mock provider. | High | SB02, SB06, SB15-SB18, SB20, SB30, and SB33 require typed no-provider proof and no silent fallback. |
| Existing migrations leave native memory tables permanently in the main DB. | Medium | SB31 explicitly handles export, compatibility, retirement, and migration policy after native service validation. |

## Validation Risks

- Tests may pass against fakes while production paths still use direct `CognitiveMemory*` services. Dependency and anti-stub source audits are required in checkpoint gates.
- UI screenshots may prove only route availability, not provider switching, iframe/RCL fallback, or zero-provider startup. UI subbundles require explicit browser validation analytics.
- A build may pass while Qdrant remains configured in base composition. SB30 requires startup proof with Qdrant disabled/unconfigured.
- Native service tests may pass with InMemory EF only. SB25 and SB29 require both InMemory and PostgreSQL profile coverage where feasible.
- Long-running operation support may be implemented as synchronous waits. SB09 requires status ledger, cancellation, polling/callback proof, and timeout behavior.
- MAF integration tests may pass by registering memory tools in a side path that current `MafAgentRuntime` never uses. SB16-SB19 must prove integration through current `IAgentRuntimeToolProvider`, `IWorkflowExecutor`, and `IAgentContextContributor` registration paths.

## Reopen Triggers

- Any new MAF project references `CanDoItAll.Modules.CognitiveMemory` or native `CanDoItAll.CognitiveMemory` implementation projects.
- Any generic memory project references Qdrant, native recall services, native UI pages, or native DB entities.
- Any provider driver directly queries `AppDbContext` for source data instead of using Source Gateway adapters.
- Any implementation adds a second independent memory operation path for tools and workflow executors instead of using the shared operation handler.
- Base CanDoItAll startup fails when Qdrant and native memory provider configuration are absent.
- Native service migration still stores native memory domain records in the main AppDbContext after SB31.
- Provider-specific UI cannot be disabled or loaded independently from generic memory UI.
- Any implementation creates a new source snapshot model without a compatibility/migration decision for `MemorySourceSnapshotContracts.cs`.
- Any zero-provider scenario reaches native Cognitive Memory, OpenAI, Qdrant, a mock provider, or an in-process compatibility adapter without explicit profile/configuration.
