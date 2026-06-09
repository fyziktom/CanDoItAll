# SB027 Semantic Invariants

Status: Passed.

## Shallow-Pass Trap

The gate is not satisfied by a completed process row, a non-empty artifact table, or a markdown summary alone. The proof must show a process scenario completes through launch/outbox dispatch, required managed artifact files can be read and contain implementation/rollout content, generated C# output exists, implementation/review route metadata is correct, and .NET build/test/run proof rules accept and reject the right evidence.

## Adversarial Negative Proof

The proof would fail if any of these regressions were introduced:

- the deterministic software-development process does not complete;
- required implementation change-set or rollout checklist artifact rows are missing;
- managed artifact files cannot be read from workspace storage;
- managed artifact files do not contain implementation, validation, rollout, and rollback content;
- generated C# output `ValidationEngine.cs` is not written;
- implementation execution no longer receives the software-development tool profile;
- review execution no longer receives the quality-validation tool profile;
- runnable .NET implementation no longer requires build/test/run tools;
- existing .NET scaffold validation cannot satisfy the scaffold guard;
- .NET web implementation can complete without runtime startup proof;
- the process mock implementation can complete without DB-free rollout checklist evidence.

## Semantic Positive Proof

`bundle://proof/SB027/transcripts/focused-dotnet-scenario-runtime-tests.txt` proves the focused P09 matrix passes against current integration-test binaries.

## Anti-Stub Proof

`bundle://proof/SB027/transcripts/anti-stub-dotnet-scenario-runtime-tests.txt` proves the process scenario uses real app services, real durable outbox dispatch, real workspace file reads, and deterministic direct-agent execution rather than report-only or mutation-shortcut proof.

## Raw-Note Closure

- RN-004 is solved for the deterministic bundle scope: SB027 proves the software-development process runtime path closes cleanly with concrete managed implementation/rollout files, generated C# output, route profile assertions, and .NET build/test/run guard coverage. It does not claim a live external `dotnet new` scaffold was executed by production automation in this phase.
- RN-007 remains partially solved: SB021/SB024/SB027 prove dispatch, MAF workflow/direct-agent, and deterministic software-development runtime compatibility. Runtime host, registry, selector, DI registration, manager command, scheduler, and workflow-driver roadmap items remain planned by SB037-SB042 and SB050-SB054.

## Production Behavior Artifact Matrix

No new production signals were introduced in SB025-SB027. Existing `.NET` template catalog entries, staffing capability signals, execution prompt tool requirements, process mock managed artifacts, generated C# output, process artifact projection, workspace file reads, and .NET completion guards are covered by focused tests and source assertions.
