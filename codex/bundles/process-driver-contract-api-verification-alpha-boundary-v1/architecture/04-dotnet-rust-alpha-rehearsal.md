# .NET / Rust Verification-Only Alpha Rehearsal

## Purpose
Prepare the first domain driver lane without production runtime implementation.

## Allowed Inputs
- existing build transcript text
- existing test transcript text
- existing bundle proof files
- existing source assertion result files
- Core descriptor snapshots supplied by caller

## Allowed Outputs
- diagnostics
- severity
- category
- evidence reference ids
- suggested next proof
- unsupported-operation denial

## Denied Behavior
- running `dotnet`, `cargo`, shell, PowerShell, bash, or any external command
- reading arbitrary workspace paths
- writing files/artifacts
- mutating process state
- invoking AgentFramework
- scheduling retries
- changing claims/transitions/finalizer behavior

## Test-Only Harness
A test fixture may simulate transcript inspection and diagnostic classification. It must live under tests and must not register production services.
