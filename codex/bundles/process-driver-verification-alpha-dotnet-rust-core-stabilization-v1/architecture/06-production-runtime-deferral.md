# Production Runtime Deferral

## Decision
This bundle implements a production verification-only alpha library, but it does not wire that package into process runtime.

## Implemented Verification-Only Surface
- `src/CanDoItAll.Processes.Drivers.TranscriptVerification`
- contract-only dependency on `CanDoItAll.Processes.Drivers.Abstractions`
- deterministic parsing of supplied .NET/Rust transcript text
- immutable response with diagnostics, evidence references, redaction metadata, audit facts, and `NoMutationPerformed = true`

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
