# Checkpoint Policy

Every checkpoint subbundle must include a refactoring pass and must not merely run tests. The implementation agent must actively inspect the code produced by the previous phase.

## Mandatory checkpoint checks

- New files are not overgrown and helper logic is extracted into cohesive classes.
- Public APIs are minimal and not leaking native implementation details.
- No duplicate mappers, serializers, operation handlers, or policy evaluators exist.
- Cancellation tokens and async APIs are propagated.
- Network calls have timeout policy and status visibility.
- Tests include failure modes, not only happy paths.
- Architecture guard tests protect dependency direction.
- Source references and docs are updated.
- Any temporary adapter is named and documented as temporary.
- Current MAF integration uses `IAgentRuntimeToolProvider`, `IWorkflowExecutor`, and `IAgentContextContributor` registration paths rather than a parallel memory-only runtime.
- Existing `MemorySourceSnapshot*` contracts are reused, rehomed, or explicitly migrated with an adapter.

## Checkpoint stop conditions

Stop downstream execution when:

- a foundation test is shallow and can pass against a stub;
- a generic memory class references native `CognitiveMemory*` classes;
- MAF references native memory classes;
- generic runtime references Qdrant;
- source adapters expose EF entities;
- UI requires the native provider to render the generic page;
- base startup fails with native memory and Qdrant disabled;
- native service still requires host AppDbContext after SB29.
- zero-provider memory scenarios call native Cognitive Memory, OpenAI, Qdrant, or mock providers without explicit provider configuration;
- a second incompatible source snapshot contract family appears without a migration adapter and tests.
