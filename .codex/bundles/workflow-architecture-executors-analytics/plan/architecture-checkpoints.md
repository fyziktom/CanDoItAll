# Architecture Checkpoints

## Before SB01

- Baseline snapshot and dependency graph recorded.
- Active duplicate contracts and consumers identified.
- No production edits until prepared-bundle validation passes.

## After SB01

- Workflows.Core no longer references Workflows.Runtime for contracts.
- Active interfaces have one definition and real consumers.
- Catalog and invoker consume the same executor contribution set.
- No service locator, new cycle, or partial-class expansion.

## After SB02 And SB03

- Source ingestion delegates document extraction to the canonical converter.
- Runtime tools and executors share operations but retain separate governance adapters.
- Every runnable contribution has descriptor, implementation, DI, schema, preview/simulation, failure, and catalog parity tests.
- Command node is runnable only if typed allow-list and approval tests pass.

## After SB04 And SB05

- All launch origins use the application launch policy.
- Running state is persisted before backend completion and cancellation behavior is explicit.
- Canonical usage observations preserve identity, correlation, token detail, pricing status, and timestamps.
- Analytics projection has producer, consumer, lifecycle, and negative proof.

## After SB06

- No executor-ID settings branches remain for ordinary schema-driven create/edit flows.
- Custom renderer activation is trusted, registered, versioned, and contract-checked.
- Large-screen UI uses existing BaseLib/CanvasLib components and shows real typed analytics.

## Final

- Architecture review gate passes.
- CodeAnalytics shows no new cycles and intended dependency direction.
- Focused suites, integration suites, solution build, and large-screen browser proof pass.
