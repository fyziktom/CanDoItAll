# SB04 Proof Manifest

## Status

- `Blocked until SB01-SB03 pass`

## Required Evidence

- API transcript for launch/provision/execute and final run detail.
- Project-structure read transcript showing final verdict/evidence node under `Main app`.
- Final app source assertions for static/no-backend output.
- Build/test/static-host proof, as applicable to the selected implementation.
- Playwright screenshot, snapshot, console, keyboard/localStorage assertion output.
- Anti-stub audit and final verifier/red-team closure artifact.

## Production Behavior Artifact Matrix

| Artifact/Signal | Producer | Consumer | Lifecycle | Required Negative Test |
| --- | --- | --- | --- | --- |
| Final process run detail | Processes API | Bundle final closure gate | Created during rerun, saved as evidence, audited for success/no escalation | Failed run or open escalation fails closure. |
| Final project-structure verdict node | Project-structure writeback step | User/project graph | Created under target `Main app` node | Missing node fails closure. |
| Final app gameplay proof | Browser validator | Final closure gate | Captured after app launch/static host | Non-interactive/static mismatch fails closure. |

## Planned Transcript Paths

- `bundle://proof/SB04/transcripts/api-rerun.txt`
- `bundle://proof/SB04/transcripts/project-structure-read.txt`
- `bundle://proof/SB04/transcripts/final-app-validation.txt`
- `bundle://proof/SB04/transcripts/red-team-closure.txt`
