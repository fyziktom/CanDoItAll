# Core And Driver Boundary Rules

## Core must not reference
- `CanDoItAll.Processes.Drivers.*`
- `CanDoItAll.Modules.*`
- `CanDoItAll.Infrastructure`
- `CanDoItAll.AgentFramework`
- EF/DbContext
- workspace/storage/filesystem
- UI/Blazor
- runtime driver registry/selector/host/provider

## Driver implementation packages must not reference
- `CanDoItAll.Modules.*`
- `CanDoItAll.Infrastructure`
- `CanDoItAll.AgentFramework`
- EF/DbContext
- UI/Blazor
- workspace/storage/file IO
- external connectors

## Allowed driver implementation dependencies
- `CanDoItAll.Processes.Drivers.Abstractions`
- `CanDoItAll.Processes.Core` only when the driver consumes deterministic descriptors directly
- BCL-only parsing/hash/redaction APIs

## Process module adapter allow-list
The only process-module files allowed to reference driver packages after this bundle should be:
- existing transcript read-only adapter and its helper files,
- new runtime evidence read-only adapter and its helper files,
- test-only helpers explicitly listed in architecture tests.

Any other import must fail architecture tests.
