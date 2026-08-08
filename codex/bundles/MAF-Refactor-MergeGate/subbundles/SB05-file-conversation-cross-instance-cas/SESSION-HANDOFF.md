# Session handoff — SB05

## Repository state

- Branch: `maf-refactor`
- Starting SHA: `d27fb8307aac6a5f88be6504eb5a284ca41ac1f6`
- Current SHA: `d27fb8307aac6a5f88be6504eb5a284ca41ac1f6`
- Worktree: dirty with cumulative SB00-SB05 implementation and proof changes; no commit was requested or created.

## Completed

- Replaced instance-local conversation gates with a process-wide canonical-document-path coordinator.
- Added reference-counted retirement and exact-entry removal so the coordinator remains bounded.
- Serialized create, get, replace, list reads, and delete across independent store instances.
- Preserved temp-file-plus-move replacement and added cleanup in a `finally` block.
- Added injected write-failure and cancellation coverage plus cross-instance create/replace/delete races.
- Release solution build passed with zero warnings and errors.

## In progress

- None for SB05.

## Blockers/failing tests

- SB05 has no targeted failure.
- Exactly four intentional downstream characterizations remain red: three SB06 and one SB07.

## Decisions

- Coordination keys use `Path.GetFullPath` and Windows path casing semantics on Windows.
- The guarantee is process-wide only; cross-process writers remain outside the contract.
- Reads take the same document lease so they cannot observe replacement/delete interleavings inside the process.
- A narrow internal write delegate supplies deterministic failure/cancellation tests without changing the public API.

## Changed files

- See `proof-manifest.json` and `../../proof/SB05/manifest.md`.

## Commands run

- See `../../proof/SB05/transcripts`.

## Next exact action

- Enter SB06 and reproduce/repair adoption compensation, active-turn rename rejection, and pre-provider transcript-capacity reservation.

## Risks not to forget

- Do not overclaim cross-process coordination.
- Do not replace exact-entry reference-counted removal with an unbounded static lock dictionary.
- Keep temp cleanup in `finally` on every writer failure or cancellation path.
