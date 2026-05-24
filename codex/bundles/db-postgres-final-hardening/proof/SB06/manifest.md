# SB06 proof manifest

## Status

Completed.

## Changed files

| File | SHA-256 before | SHA-256 after | Reason |
|---|---|---|---|
| None | n/a | n/a | Source proof only. |

## Commands

| Command | Result | Transcript |
|---|---|---|
| Process dispatch claim-first source audit | Passed | `transcripts/process-dispatch-claim-first-source-audit.txt` |
| Process dispatch claim-first context capture | Passed | `transcripts/process-dispatch-claim-first-context.txt` |

## Source assertions

| Assertion | Source | Proof |
|---|---|---|
| Process dispatch loads candidate headers before claim and loads full candidate only after `TryClaimStepDispatchAsync` succeeds. | Process dispatch service source | Context transcript. |
| Lease renewal callback throws when dispatch claim ownership is lost. | Process dispatch service source | Context transcript. |

## Negative tests

| Scenario | Expected | Result |
|---|---|---|
| Hidden full-run candidate load before claim. | Source audit should find claim-first shape. | Passed. |

## Remaining risks

No code change was made in SB06. It remains a source-level proof backed by focused source context, not a runtime query counter.
