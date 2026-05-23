# SB03 Proof Manifest

## Status

- `Not started`

## Required Evidence

- Changed-file hashes for validation prompt/rule changes.
- Negative proof transcript showing the captured bad app fails semantic browser criteria.
- Positive proof transcript or test fixture showing a corrected interactive game passes.
- Playwright artifact paths for screenshot, snapshot, console, and keyboard/localStorage assertion output.
- Anti-stub audit transcript.

## Production Behavior Artifact Matrix

| Artifact/Signal | Producer | Consumer | Lifecycle | Required Negative Test |
| --- | --- | --- | --- | --- |
| Browser semantic assertion record | Browser validation step | Process completion evaluator and final evidence index | Captured during validation, written as durable evidence | Static DOM with `Status Loading` fails. |
| Local high-score persistence proof | Browser/localStorage check | QA validation and final verifier | Created after gameplay event, reread before closure | localStorage null/unchanged fails. |

## Planned Transcript Paths

- `bundle://proof/SB03/transcripts/bad-app-negative-proof.txt`
- `bundle://proof/SB03/transcripts/positive-proof.txt`
- `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
