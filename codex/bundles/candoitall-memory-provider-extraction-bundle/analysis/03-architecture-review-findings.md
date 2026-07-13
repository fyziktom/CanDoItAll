# Architecture Review Findings

## Findings retained from the previous architecture

- The correct conceptual split is generic memory integration in CanDoItAll plus native Cognitive Memory as one optional provider.
- The protocol must support simple query-response providers and advanced eventful providers.
- Provider selection must be configured per agent, workflow, process, or runtime context.
- Tool and workflow executor logic must share the same operation service.
- Source ingestion requires a source gateway and provider-specific ingestion jobs.
- Feedback correlation must survive beyond a single request and support late outcome feedback.
- UI must have common surfaces plus provider-specific RCL or iframe projection.
- Native memory needs its own DB and EF model.

## Findings added by this bundle review

- The current code requires an in-process adapter stage before physical service extraction because API, UI, MAF integration, domain, EF, and Qdrant dependencies are intertwined.
- The main host composition currently pulls native memory into module assembly discovery, database model registration, and service startup. Dependency removal must be a dedicated final phase, not assumed to happen during migration.
- Existing `CognitiveMemoryMafIntegration.cs` mixes context contribution, workflow executor settings, native recall/probe calls, serialization, and output shaping. It should not be refactored directly into generic MAF; instead generic memory operation contracts should be built first and old native executors should be replaced through adapter compatibility.
- Current memory services use `IDbContextFactory<AppDbContext>` across many files. Native DB extraction is likely the hardest technical risk and should have its own phase and checkpoint.
- Current tests are valuable but coupled to native names and AppDbContext. Rebalancing tests into generic and native suites must be planned explicitly.
- The 2026-07-05 re-entry found existing MAF memory source snapshot contracts and Workbench/Workflow providers. The generic Source Gateway must reuse, rehome, or deliberately migrate those contracts; a second source snapshot family would be an architectural regression.
- The current native repo exists but is unscaffolded, so SB24 is a real scaffold phase, not just a target-repo verification step.
- The generic memory provider module must treat zero configured memory providers as a normal state. Startup, UI, tools, workflow executors, and context contribution must fail predictably or skip by policy without falling back to native memory, OpenAI, Qdrant, or test mocks.
