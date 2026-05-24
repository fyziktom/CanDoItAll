# SB07 proof manifest

## Status

Completed.

## Owned requirements

Move process dispatch toward claim-first candidate loading and instrument candidate discovery/hydration.

## Semantic invariant contract

`bundle://proof/SB07/semantic-invariants.md`

## Changed files

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
- Hash proof: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`

## Command transcripts

- `bundle://proof/SB07/transcripts/claim-first-source-audit.txt`
- `bundle://proof/SB07/transcripts/process-dispatch-telemetry-build.txt`
- `bundle://proof/SB08/transcripts/focused-integration-tests.txt`

## Source assertions

- Dispatch now loads lightweight candidate headers with `LoadDispatchCandidateHeadersAsync`.
- Dispatch claims a specific step by durable token before hydrating full candidate details.
- If hydration fails after claim, the claim is released and the scheduler continues to the next header.
- Telemetry logs elapsed time for header discovery and claimed candidate hydration.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Dispatch candidate header | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs` | dispatch claim/hydration flow in the same file | `bundle://proof/SB07/transcripts/process-dispatch-telemetry-build.txt` | `bundle://proof/SB07/transcripts/claim-first-source-audit.txt` |

## Semantic positive proof

Focused integration tests pass with the claim-first dispatch flow, and the process module builds with the telemetry additions.

## Adversarial negative proof

Hydration failure after claim releases the durable claim before trying the next candidate, preventing a poisoned header from blocking later eligible work.

## Residual risks

The requested query-count metric was not captured. Elapsed-time telemetry was added and the source audit proves the heavy-load position moved after durable claim.
