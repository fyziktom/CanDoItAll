---
name: csharp-provider-tool-plugin-isolation
description: "Isolate C# tools, providers, plugins, memory providers, MCP connectors, workflow executors, and process drivers into extension-friendly contracts, implementation projects, catalogs, adapters, factories, and tests."
---

# C# Provider, Tool, Plugin, and Memory Isolation

Use this skill whenever adding or refactoring a tool, provider, plugin, memory protocol implementation, MCP connector, workflow executor, capability provider, or process driver.

## Goal

New extensions should be added by creating or registering a provider/tool/driver module, not by editing a large runtime partial class.

## Required extension model

A good extension model has:

- stable contract project
- implementation project when SDKs or distinct lifecycle are involved
- typed metadata or descriptor
- adapter for external SDK/protocol details
- catalog provider or registration extension
- factory or selector when runtime choice is needed
- isolated unit tests
- composition smoke tests

## Tool isolation checklist

When adding a tool:

- Define the tool contract and metadata in the appropriate abstractions/tooling project.
- Put concrete tool behavior in a dedicated tool implementation type.
- Register the tool through a catalog provider or module registration method.
- Keep security and access policy as decorators, validators, or pipeline handlers.
- Keep runtime orchestration unaware of every concrete tool class.
- Add tests for metadata, input validation, execution behavior, error behavior, and registration.

## Provider isolation checklist

When adding a provider:

- Keep provider-specific SDK types out of contracts.
- Create an adapter that maps provider SDK models into repository-owned records.
- Put provider configuration in typed options.
- Use a factory or keyed provider catalog for selection.
- Add fake provider tests for runtime behavior.
- Add provider-specific integration tests only where external dependencies are intentionally used.

## Memory protocol checklist

When adding memory providers:

- Put protocol contracts in an Abstractions project.
- Put provider implementation in its own project if it brings provider-specific SDKs or transport dependencies.
- Keep serialization and mapping explicit and testable.
- Add unit tests for provider-independent protocol behavior.
- Add provider adapter tests with fake responses.
- Ensure the runtime does not branch on provider-specific names except through a factory or catalog.

## Process driver checklist

When adding process drivers:

- Keep generic process core independent from domain-specific driver behavior.
- Put driver contracts in Drivers.Abstractions.
- Put standard or domain drivers in implementation projects.
- Register drivers through a driver catalog.
- Test driver decisions independently from the full process runtime.
- Add one runtime smoke proving driver discovery and invocation through the generic contract.

## Common bad pattern

```text
MafAgentRuntime.Tools.cs
MafAgentRuntime.Memory.cs
MafAgentRuntime.Plugins.cs
MafAgentRuntime.Mcp.cs
```

This is file-level grouping, not architecture. It still forces future extensions through the runtime type.

## Better pattern

```text
Tools.Abstractions
Tools.Standard
Tools.Documents
Memory.Abstractions
Memory.Mem0
Memory.Qdrant
Processes.Drivers.Abstractions
Processes.Drivers.Standard
Composition module registration
```

## Proof required

- extension can be added without editing the old runtime class
- contract project remains SDK-free
- provider/tool implementation has direct unit tests
- runtime uses catalog/factory/abstraction
- composition smoke proves registration
- anti-stub audit rejects placeholder descriptors with no production path

## Exit condition

Exit only when the extension surface is modular, independently testable, provider-specific dependencies are isolated, and future additions do not require another runtime partial class.
