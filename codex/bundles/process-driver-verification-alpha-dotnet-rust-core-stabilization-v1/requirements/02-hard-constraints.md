# Hard Constraints

## Production Runtime Denials
The implementation must not add any of the following in production source:
- driver runtime registry
- driver selector
- dependency-injection extension or service registration for driver runtime
- manager command
- shell/process execution
- package restore
- Graph/Office connector operation
- workspace/storage write
- process mutation
- claim/lease mutation
- transition execution
- finalizer application
- retry scheduling
- provider repair
- UI/browser changes

## Allowed Production Movement
Allowed:
- A verification-only alpha implementation package or module-local service that consumes already-provided transcript/evidence content and returns diagnostics.
- Immutable request/response/diagnostic/audit objects from existing driver abstractions.
- Pure parser/classifier code that operates on strings and metadata only.
- Tests, fixtures, docs, and source-scan guards.

## Required Proof
- `dotnet build CanDoItAll.slnx --no-restore` must pass with zero warnings and zero errors.
- Full unit tests must pass.
- Focused process/Core/driver tests must pass.
- Source scans must prove no forbidden runtime driver tokens, no forbidden Core deps, no UI/media drift, no stub markers.
- Prepared and completed bundle validators must pass.
