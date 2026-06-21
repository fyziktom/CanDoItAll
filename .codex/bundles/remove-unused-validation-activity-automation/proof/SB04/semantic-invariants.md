# SB04 Semantic Invariant Contract

## Invariant ID

- Invariant ID: `RM-004`

## Source raw note

- Assure the app works again, rebuild the running 5032 instance, and test it.

## Expected behavior

- The solution builds, targeted tests pass, port `5032` serves the rebuilt app, and Browser proof shows no retired module routes or labels.

## Disallowed shallow implementation

- Reporting success without a rebuilt local host, without Browser verification, or with the old routes still visible.

## Failing-first test

- failing-first: N/A - process/non-production final verification; this subbundle validates the completed removal rather than adding a new behavior fixture.

## Passing test

- `proof/SB04/transcripts/build-solution.txt`
- `proof/SB04/transcripts/test-components-targeted.txt`
- `proof/SB04/transcripts/test-unit-targeted.txt`
- `proof/SB04/transcripts/test-integration-service-targeted.txt`
- `proof/SB04/transcripts/browser-final-restart-check.json`

## Changed source files

- Bundle proof and report files.

## Production assertions

- Final build exits `0`.
- Targeted component/unit/service integration tests exit `0`.
- Final Browser check reports Scheduler/Test Lab present, removed labels absent, retired route text absent, and Blazor error UI hidden.

## Red-team negative case

- Browser proof would fail if retired validation, activity, or automation routes persisted in restored shell state.

## Downstream dependency check

- This is the final downstream closure gate for SB01, SB02, and SB03.
