# SB02 Proof Manifest

## Status

- Subbundle: `SB02-office365-live-validation`
- Closure decision: `Completed`

## Live Validation Inputs

- Local app/API root: `bundle://proof/SB02/transcripts/app-reachability.txt`
- Workflow discovery: `bundle://proof/SB02/transcripts/workflow-discovery.txt`
- Project target discovery: `bundle://proof/SB02/transcripts/project-target-discovery.txt`
- Project-structure workflow node baseline: `bundle://proof/SB02/transcripts/project-structure-read.txt`
- Original failed run evidence: `bundle://proof/SB02/transcripts/pre-fix-failed-run-evidence.txt`

## Live Validation Result

- Live run transcript: `bundle://proof/SB02/transcripts/office365-live-validation.txt`
- Sanitized event proof: `bundle://proof/SB02/transcripts/office365-live-validation-event-proof.txt`

## Assertions

| Assertion | Evidence |
| --- | --- |
| The local app/API was reachable and API auth was disabled. | `bundle://proof/SB02/transcripts/app-reachability.txt` |
| The target workflow was the seeded `Example: Office365 Category Email Summary To Project`. | `bundle://proof/SB02/transcripts/workflow-discovery.txt` |
| The same project-structure workflow node that previously failed was rerun. | `bundle://proof/SB02/transcripts/project-structure-read.txt` and `bundle://proof/SB02/transcripts/office365-live-validation.txt` |
| Live run `fe41c9d6-d2ea-4127-b2c0-33a7ba9ab9bf` completed. | `bundle://proof/SB02/transcripts/office365-live-validation.txt` |
| `summarize-office365`, `store-office365-summary`, and `mark-office365-processed` completed. | `bundle://proof/SB02/transcripts/office365-live-validation-event-proof.txt` |
| The invalid JSON failure marker count was `0`. | `bundle://proof/SB02/transcripts/office365-live-validation-event-proof.txt` |
| A project-structure summary asset node was created. | `bundle://proof/SB02/transcripts/office365-live-validation-event-proof.txt` |

## Gate Result

- Entry gate: passed after SB01 closure proof and app/API reachability.
- Closure gate: passed. Final bundle closure may proceed.
