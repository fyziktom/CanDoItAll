# SB018 Semantic Invariants

## Status
Completed.

## Invariant SB018_INV_001
- Invariant ID: `SB018_INV_001`
- Source raw note: "Review real code, not only bundle report" and "Determine real test outcome."
- Expected behavior: The deterministic .NET process scenario creates a concrete C# file, modifies it through repair, records implementation and rollout artifacts, completes the process run, and exposes managed artifact readback.
- Disallowed shallow implementation: Treating any artifact row or successful mock provider response as .NET scenario completion.
- Failing-first test: `bundle://proof/SB018/red-team/generic-artifact-only-proof-rejection.txt` rejects generic-artifact-only closure.
- Passing test: Four focused integration tests passed in `bundle://proof/SB018/transcripts/dotnet-process-scenario-tests.txt`.
- Changed source files: No production source changed in SB018. Current source hashes are captured in `bundle://proof/SB018/manifest.md`.
- Production assertions: `bundle://proof/SB018/transcripts/dotnet-process-scenario-source-assertions.txt` cites `ValidationEngine.cs`, implementation change-set, rollout checklist, deterministic repair, completed process run, and artifact readback assertions.
- Red-team negative case: `bundle://proof/SB018/red-team/generic-artifact-only-proof-rejection.txt`
- Downstream dependency check: Live OpenAI proof may start only after deterministic local/fake-provider scenario completion is established.

## Shallow-Pass Trap
A fake Gate F closure could list an artifact count or mock response. SB018 rejects that by requiring concrete C# file creation, repair mutation, implementation artifacts, migration/rollout artifacts, managed file readback, and completed process state.

## Semantic Positive Proof
- `bundle://proof/SB018/transcripts/dotnet-process-scenario-tests.txt`
- `bundle://proof/SB018/transcripts/dotnet-process-scenario-source-assertions.txt`

## Adversarial Negative Proof
- `bundle://proof/SB018/red-team/generic-artifact-only-proof-rejection.txt`

## Anti-Stub Audit
- `bundle://proof/SB018/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Matches are documentation and negative test assertions, not an execution-capable process-driver runtime host, process-driver registry, selector, or production `NotImplemented` path.

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `ValidationEngine.cs` | Mock .NET process execution | Managed storage readback and QA validation | Created with `namespace MockApp`, then repaired with `throw new ArgumentException` signal | Generic-artifact-only red-team rejects rows without file/content proof |
| Change-set/checklist artifacts | Developer process execution | Artifact projection and QA handoff | Persisted as managed artifacts with expected titles and content | Tests assert title/path/content, not just count |
| Completed process run | Durable outbox process runtime | Runtime readback and later UI proof | Run and expected steps complete through process runtime path | Skipped branch and completed branch assertions reject happy-path-only proof |
