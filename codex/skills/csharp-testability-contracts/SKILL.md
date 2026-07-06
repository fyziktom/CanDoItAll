---
name: csharp-testability-contracts
description: "Define and validate C# testability contracts for architecture refactoring, extracted services, providers, tools, builders, factories, adapters, drivers, and runtime composition."
---

# C# Testability Contracts

Use this skill when architecture work claims to improve testability or when extracted behavior must be proven outside the original runtime class.

## Goal

Tests must prove behavior, not just compilation or DI resolution.

## Required test layers

### Characterization tests

Use when moving existing behavior whose current semantics must remain stable.

These tests should lock observable behavior before code is moved. They can be broad if the current code is tightly coupled, but the refactor should then add narrower tests for the extracted owner.

### Isolated unit tests

Use for:

- extracted services
- strategies
- builders
- factories
- validators
- adapters with fake provider responses
- provider-independent protocol behavior
- process drivers
- tool execution logic

Unit tests should avoid filesystem, database, network, external APIs, real clocks, and full app hosts unless the unit explicitly abstracts them.

### Negative tests

Use to prove shallow implementations fail:

- unsupported provider id throws a domain-specific exception
- missing required builder field fails validation
- invalid tool input is rejected
- provider adapter does not accept malformed response
- process driver refuses unsupported state
- runtime factory does not fall back to a default provider silently

### Composition smoke tests

Use when registration or project wiring changed:

- DI resolves catalog/factory
- registered descriptors include expected extension
- runtime calls through abstraction
- provider implementation is not directly referenced by core

## Test naming

Prefer names that state behavior:

```csharp
Create_UnknownProviderId_ThrowsUnsupportedMemoryProviderException()
Build_MissingRequiredToolName_ReturnsValidationError()
Execute_InvalidToolInput_DoesNotInvokeProvider()
```

Avoid:

```csharp
Test1()
FactoryWorks()
ShouldPass()
```

## Test seam requirements

A testability claim is invalid if:

- tests instantiate the original large runtime for extracted behavior
- tests require live external provider credentials for unit behavior
- tests manually seed production-only state that no production path emits
- tests assert only non-null descriptors
- tests do not include a failure case
- tests verify implementation details instead of observable behavior

## Required proof

- test list with purpose
- failing-first or characterization proof when behavior changed or moved
- passing transcript
- source reference to extracted type under test
- statement of what dependency was faked
- statement of what remains integration-only

## Exit condition

Exit only when the architecture change produces smaller, faster, more isolated tests and those tests would fail if behavior remained in the old monolith.
