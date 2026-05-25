# SB07 semantic invariants

## SB07-I4 claim-first candidate loading

- Source raw note: avoid loading full process run state before acquiring durable dispatch ownership.
- Expected behavior: discover minimal eligible headers, claim one step, then hydrate the claimed step.
- Disallowed shallow implementation: keeping `LoadDispatchCandidateAsync` as the first operation for every eligible step.
- Passing proof: `bundle://proof/SB07/transcripts/process-dispatch-telemetry-build.txt` and `bundle://proof/SB08/transcripts/focused-integration-tests.txt`.
- Changed source files and hashes: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB07/transcripts/claim-first-source-audit.txt`.
- Red-team negative case: claimed candidate hydration failure releases the claim before continuing.
- Downstream dependency check: `bundle://proof/SB08/transcripts/semantic-invariant-index.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Dispatch candidate header | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | dispatch claim/hydration flow | `bundle://proof/SB08/transcripts/focused-integration-tests.txt` | `bundle://proof/SB07/transcripts/claim-first-source-audit.txt` |
