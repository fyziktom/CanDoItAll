# Responsibility Slicing Rubric

Use this rubric before refactoring a large C# class or adding new behavior to an existing runtime class.

## Responsibility categories

Classify each method, field, nested type, and constructor dependency into one primary responsibility:

- composition and dependency wiring
- runtime orchestration
- workflow or process execution
- provider selection
- provider invocation
- tool definition metadata
- tool execution
- plugin discovery
- capability catalog construction
- memory protocol contracts
- memory provider implementation
- persistence
- serialization and mapping
- retry, resiliency, and recovery
- approval and security policy
- telemetry and metrics
- UI projection
- test fixture or seed behavior

## Split signals

A responsibility is a candidate for extraction when at least one is true:

- it can be unit-tested without the original runtime class
- it has its own lifecycle or configuration
- it can have multiple implementations
- it maps to an existing project group such as Abstractions, Core, Application, Runtime, Drivers, Providers, Tools, Skills, Persistence, or Projections
- it requires external SDKs that should not leak into core contracts
- it changes for a different reason than the parent class
- it is likely to grow when new tools/providers/drivers are added
- it has its own failure modes and proof requirements

## Extraction target choices

Choose the narrowest real boundary that prevents re-growth:

- Extract method only when the behavior still belongs to the same responsibility.
- Extract class when the behavior has one responsibility but belongs in the same project.
- Extract interface when callers need to depend on a stable contract.
- Extract project when the contract or implementation needs independent references, tests, or SDK/package isolation.
- Extract factory when construction chooses among implementations.
- Extract builder when a complex definition is assembled step by step and validated before use.
- Extract strategy when algorithms or execution paths are interchangeable.
- Extract adapter when an external SDK or protocol is being normalized.
- Extract facade only when it is thin and delegates to cohesive services.

## Anti-goal

Do not extract by file size alone. Extract by responsibility, dependency direction, and test seam.
