# Session handoff — SB04

## Repository state

- Branch: `maf-refactor`
- Starting SHA: `d27fb8307aac6a5f88be6504eb5a284ca41ac1f6`
- Current SHA: `d27fb8307aac6a5f88be6504eb5a284ca41ac1f6`
- Worktree: dirty with cumulative SB00-SB04 implementation and proof changes; no commit was requested or created.

## Completed

- Replaced the fixed-scope cleaner with persisted-run scope resolution plus a typed scope factory.
- Added fail-closed metadata/governance conflict and profile/agent validation.
- Moved concrete service construction to Hosting and Modules.AgentFramework composition.
- Limited cleanup composition to a process host and command service and disposed the owned scope.
- Proved completion, approval continuation, failure, organization/sandbox compatibility, conflict retry, and concurrent idempotency.
- Release solution build passed with zero warnings and errors.

## In progress

- None for SB04.

## Blockers/failing tests

- SB04 has no targeted failure.
- Exactly five intentional downstream characterizations remain red: one SB05, three SB06, and one SB07.
- The filtered full Unit sweep hit the command time ceiling and must be repeated at SB09.

## Decisions

- Persisted governance is the primary effective-scope source; valid recorded context scope must agree with it.
- Trusted process metadata may supply scope for a governance-free compatible run; otherwise cleanup uses the configured storage scope.
- Scope conflicts fail before factory creation, leaving durable leases untouched for retry.
- Cleanup outcome cannot alter the already-persisted terminal run outcome.

## Changed files

- See `proof-manifest.json` and `../../proof/SB04/manifest.md`.

## Commands run

- See `../../proof/SB04/transcripts`.

## Next exact action

- Enter SB05 and reproduce the independent-instance file conversation CAS race before changing persistence.

## Risks not to forget

- Do not reintroduce a command-service-bound cleaner.
- Do not trust free-form payload scope or accept disagreement between metadata and governance.
- Do not move lease semantics into MAF.
