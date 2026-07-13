# Testing And Mocking Strategy

## Test layers

- **Unit tests**: protocol validators, provider selection, source request mapping, feedback lifecycle, event loop guard, driver factory selection, timeout/cancellation policy.
- **Contract tests**: every driver must satisfy Memory Protocol v1 scenarios for sync query, async query, ingestion, feedback, health, and unsupported capability failure.
- **Architecture tests**: dependency guards for MAF, generic memory, composition, native service, Qdrant, and AppDbContext ownership.
- **Integration tests**: generic memory persistence, source gateway adapters, operation workers, event inbox/outbox, native service API, native DB profiles.
- **Component/browser tests**: generic UI provider list, query/chat, operations, feedback, event inbox, RCL/iframe provider surface projection.
- **End-to-end tests**: startup without Qdrant/native memory, two mock providers with different agents, workflow executor memory recall, native provider remote driver, delayed feedback after process completion.
- **Re-entry architecture tests**: current MAF integration through `IAgentRuntimeToolProvider`, `IWorkflowExecutor`, and `IAgentContextContributor`; no duplicate `MemorySourceSnapshot*` contract family without a migration adapter.

## Required mock providers

- `ImmediateContextMockMemoryProvider`: returns deterministic context pack immediately.
- `DelayedContextMockMemoryProvider`: returns accepted operation and completes after status polling.
- `EventfulMockMemoryProvider`: emits hypothesis/source-request/feedback-request events.
- `FailingMockMemoryProvider`: simulates timeout, unsupported capability, auth failure, and malformed response.
- `UiSurfaceMockMemoryProvider`: declares RCL/iframe/no-ui surfaces for UI projection tests.

Mock providers must be explicitly configured in each test scenario. They must not be registered as default fallback providers for production or zero-provider tests.

## Proof philosophy

Positive happy-path proof is not enough. Critical foundation subbundles require failing-first tests, adversarial negative cases, anti-stub source audits, and downstream smoke tests before later phases start.

Zero-provider proof is required in foundation, runtime, MAF, UI, host decoupling, and final e2e gates. It must assert that no operation reaches native Cognitive Memory, OpenAI, Qdrant, or a mock provider unless a provider profile was explicitly configured.
