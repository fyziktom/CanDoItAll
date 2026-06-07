# Production Runtime Deferral

## Decision
This bundle may implement a production verification-only alpha library, but it must not wire it into process runtime.

## Deferred
- process-driver registry
- dependency-injection runtime registration
- manager command
- scheduler/workflow trigger
- process runtime selector
- driver execution host
- external connectors

## Future Runtime Prerequisites
- persistent audit store
- capability gate
- caller identity binding
- version compatibility
- denial telemetry
- allow-list and sandbox policy
- integration red-team
