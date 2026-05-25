# SB06 semantic invariants

## SB06-I4 claim token owns mutation rights

- Source raw note: a stale long-running worker must not commit after its durable claim expired or was stolen.
- Expected behavior: renewal failure stops the worker, and every artifact/transition/failure mutation verifies the same unexpired claim token.
- Disallowed shallow implementation: logging renewal failure while continuing mutation work under only a process-local semaphore.
- Passing proof: `bundle://proof/SB08/transcripts/focused-integration-tests.txt`.
- Changed source files and hashes: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB06/transcripts/dispatch-claim-source-audit.txt`.
- Red-team negative case: source audit proves claim checks are in production mutation paths, not only fixtures.
- Downstream dependency check: `bundle://proof/SB08/transcripts/semantic-invariant-index.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `ProcessStepDispatchClaim` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs` | `bundle://proof/SB08/transcripts/focused-integration-tests.txt` | `bundle://proof/SB06/transcripts/dispatch-claim-source-audit.txt` |
