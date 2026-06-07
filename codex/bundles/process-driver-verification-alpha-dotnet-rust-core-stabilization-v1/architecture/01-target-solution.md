# Target Solution

## Current Target
Create a first verification-only alpha driver implementation for `.NET/Rust transcript verification`.

## Allowed Shape
Preferred production shape:
- `src/CanDoItAll.Processes.Drivers.TranscriptVerification`
- references `CanDoItAll.Processes.Drivers.Abstractions` only, and optionally `CanDoItAll.Processes.Core` only for descriptor names if needed
- exposes pure parsing/classification services over supplied strings and immutable request objects
- returns `ProcessDriverVerificationResponse`
- produces audit fact and redaction descriptors
- never performs IO, shell, package restore, workspace/storage writes, process mutation, or external calls

## Denied Shape
Do not create:
- registry, selector, runtime, host, provider, manager command
- DI extension method
- executor that can be scheduled by process runtime
- shell/command runner
- Graph/Office connector
- workspace/storage writer
- process mutator
- finalizer/transition/claim/retry owner
