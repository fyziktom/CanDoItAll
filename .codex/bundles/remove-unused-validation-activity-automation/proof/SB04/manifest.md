# SB04 Proof Manifest

- Status: `Completed`
- Invariant: `RM-004`
- Semantic contract: `proof/SB04/semantic-invariants.md`
- Passing transcript: `proof/SB04/transcripts/build-solution.txt`
- Passing transcript: `proof/SB04/transcripts/test-components-targeted.txt`
- Passing transcript: `proof/SB04/transcripts/test-unit-targeted.txt`
- Passing transcript: `proof/SB04/transcripts/test-integration-service-targeted.txt`
- Passing transcript: `proof/SB04/transcripts/port-5032-restart.txt`
- Passing transcript: `proof/SB04/transcripts/browser-final-restart-check.json`
- Portable transcript: `bundle://proof/SB04/transcripts/build-solution.txt`
- Portable transcript: `bundle://proof/SB04/transcripts/browser-final-restart-check.json`
- failing-first: N/A - process/non-production final verification; this subbundle validates removal and host health.
- Anti-stub audit: `proof/SB04/transcripts/anti-stub-audit.txt`
- SHA-256 `proof/SB04/transcripts/build-solution.txt`: `DEFC01564BAAD15EE1B9DD6D954CE68315620D8D2278321DAD4533FCF310C433`
- SHA-256 `proof/SB04/transcripts/browser-final-restart-check.json`: `5EEFCB3D2D55C0F784348A96BA47C8BDCC76FD91A7DC009DCC21DB411986FA2B`

## Outcome

- Port `5032` is running the rebuilt app.
- Browser proof confirms the old module entries are gone and current Scheduler/Test Lab paths render.
