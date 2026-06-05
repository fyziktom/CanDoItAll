# Assumptions And Risks

## Assumptions

- The branch is `maf-processes-refactor`.
- The previous subprocess boundary implementation has been pushed and builds.
- `ImplementationProof.cs` is still the next highest-value domain-specific seam.
- Existing integration tests around process dispatch and artifact validation are the primary safety net.

## Critical Path Risks

- Behavior drift in repair/retry logic, domain-specific assumptions leaking into generic process semantics, premature production driver APIs, and false compile-only parity are the critical path risks.

1. **Behavior drift in repair/retry logic**  
   Implementation-proof summaries affect whether a step becomes Completed, Blocked, Failed, or retried. A small message or ordering change can alter process behavior.

2. **Domain-specific assumptions leaking into generic process semantics**  
   .NET, JS, Blazor, `.csproj`, `dotnet run`, and host shape checks must be isolated as module-local evidence rules, not silently promoted into process core vocabulary.

3. **Driver readiness becoming premature driver API**  
   The bundle may document future driver evidence families but must not create production driver interfaces, registries, or packages.

4. **False parity from compile-only extraction**  
   Helper extraction must be backed by focused tests for mutation/read ordering, carry-forward proof, runnable-host proof, stack negation, and process mock proof.

## Validation Risks

- Existing tests may be broad but not focused on subtle receipt-ordering behavior.
- Source scans must distinguish valid documentation-only driver-readiness files from production driver API.
- Some helper names may look generic but still use `.NET` concepts; this is acceptable only in module-local `DotNet` or `Stack` rule helpers.

## Reopen Triggers

- Any focused test around missing implementation proof, runnable app proof, dotnet host, process mock proof, or carried proof changes behavior.
- `Process Core`, `ProcessDriver`, `DriverPack`, `IProcessDriver`, or driver registry tokens appear under production `src`.
- Any UI file is changed without explicit user approval.
- `ImplementationProof.cs` grows materially without isolating responsibilities.
