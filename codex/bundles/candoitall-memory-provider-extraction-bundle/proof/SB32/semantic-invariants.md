# SB32 Semantic Invariants

## SB32-I01 Generic Mock Providers Are Explicit

- Mock providers are test fixtures and provider profiles, not production defaults.
- Zero-provider tests must continue to assert that no mock, native, Qdrant, OpenAI, or fallback provider is registered implicitly.
- Proof: `SB32_MP001`, `SB32_MP002`, `SB32_CP001`, and existing zero-provider tests in `bundle://proof/SB32/transcripts/passing-generic-memory-tests.txt`.

## SB32-I02 Mock Fixture Exercises Real Generic Driver Interfaces

- `GenericMockMemoryProviderFixture` implements `IMemoryProviderDriver`, `IMemoryProviderOperationStatusDriver`, `IMemoryProviderFeedbackDeliveryDriver`, `IMemoryProviderEventPollDriver`, `IMemoryProviderEventOutboxDriver`, and `IMemoryProviderHealthDriver`.
- It is not a DTO-only helper and does not depend on native Cognitive Memory module types.
- Proof: `bundle://proof/SB32/transcripts/semantic-invariant-assertions.txt` and `bundle://proof/SB32/transcripts/source-boundary-audit.txt`.

## SB32-I03 Runtime Behavior Covers Immediate, Delayed, Feedback, And Events

- Immediate context dispatch must go through `IMemoryRuntimeService` and the shared `IMemoryOperationHandler`.
- Accepted async operations must persist as running operations and complete through the status worker.
- Delayed feedback must be accepted through the shared handler and delivered through a feedback driver.
- Provider events must dedupe inbox records and drain acknowledgements through the outbox driver.
- Proof: `SB32_MP003`, `SB32_MP004`, and `SB32_MP005` in `bundle://proof/SB32/transcripts/passing-generic-memory-tests.txt`.

## SB32-I04 Test Ownership Is Classified

- Generic provider protocol/runtime/Source Gateway/MAF/driver tests live in `tests/Memory/CanDoItAll.Memory.Tests`.
- Generic Memory UI/component tests live in component and Playwright projects.
- Legacy `CognitiveMemory*` tests and `CognitiveMemoryFakes.cs` remain native-suite coverage until final native cleanup.
- `CognitiveMemoryModuleRegistrationTests.cs` is not base-host coupling proof; base-host decoupling is guarded by `HostCompositionDependencyRemovalTests`.
- Proof: `repo://docs/cognitive-memory/operations/memory-test-suite-rebalance.md` and `bundle://proof/SB32/transcripts/test-inventory-classification-audit.txt`.

## SB32-I05 Generic Tests Do Not Reintroduce Native Coupling

- The generic memory test project must not import or reference native Cognitive Memory, Qdrant, or SemanticCompletion driver namespaces.
- Generic memory source projects must remain free of native Cognitive Memory/Qdrant/SemanticCompletion driver references.
- Proof: `bundle://proof/SB32/transcripts/source-boundary-audit.txt`.

## SB32-I06 UI Coverage Remains Generic

- MemoryProvider UI/component tests cover zero-provider state, explicit demo providers, provider details, async accepted operations, feedback, event inbox, manual ingestion, RCL/iframe surfaces, disabled providers, and unsafe URL fallbacks.
- This subbundle does not add a browser-visible surface; component proof is enough for SB32.
- Proof: `bundle://proof/SB32/transcripts/passing-memory-provider-component-tests.txt`.
