# Core Compatibility Roadmap

## Core Remains Owner Of
- route descriptors
- artifact snapshots/matchers
- subprocess pure rules
- execution/finalizer/retry/projection evidence descriptors

## Drivers May Reference
- driver abstractions
- immutable Core descriptor names only when necessary

## Core Must Not Reference
- driver abstractions
- driver implementation packages
- process module
- infrastructure
- AgentFramework
- UI

## Future Rule
Every new Core public type must:
- update API inventory
- update architecture guard
- include forbidden dependency scan proof
- include module adapter proof if consumed by Processes
