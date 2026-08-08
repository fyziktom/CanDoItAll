# Session handoff — SB06

## Repository state

- Branch: `maf-refactor`
- Starting SHA: `d27fb8307aac6a5f88be6504eb5a284ca41ac1f6`
- Current SHA: `d27fb8307aac6a5f88be6504eb5a284ca41ac1f6`
- Worktree: dirty with cumulative SB00-SB06 implementation and proof changes; no commit was requested or created.

## Completed

- Added bounded durable active-turn compensation for pre-adoption provider and acceleration state.
- Added admitted revision/timestamp identity and exact pending user-entry validation.
- Routed provider failure, cancellation, explicit abandonment, and persisted crash recovery through one compensation constructor.
- Rejected rename while a turn is active and reserved both transcript slots before provider invocation.
- Enforced unique entry ids and upgraded file schema to version 2; unsafe legacy active turns fail typed.
- Documented and tested delete as an explicit terminal operation during an active turn.
- Release solution build passed with zero warnings and errors.

## In progress

- None for SB06.

## Blockers/failing tests

- SB06 has no targeted failure.
- Exactly one intentional downstream characterization remains red: SB07 attempt-usage aggregation.

## Decisions

- Compensation is stored only when provider/model adoption changes the durable snapshot.
- A schema-v1 idle document remains readable; a schema-v1 active turn fails closed because its pre-turn state is unknowable.
- Active-turn revision must equal the document revision and its pending entry must be the final user entry with exact id, turn, and timestamp.
- Failure compensation ignores caller cancellation and retries bounded CAS conflicts; it never silently swallows a failed final compensation.
- Delete remains terminal during an active turn; the canceled invocation cannot recreate the deleted document.

## Changed files

- See `proof-manifest.json` and `../../proof/SB06/manifest.md`.

## Commands run

- See `../../proof/SB06/transcripts`.

## Next exact action

- Enter SB07 and repair checked usage aggregation across every bounded provider attempt and typed failure.

## Risks not to forget

- Do not reconstruct pre-turn provider or acceleration from admitted state.
- Do not relax exact active-turn identity or two-slot admission.
- Keep schema-v1 active turns fail closed.
- Do not add ordinary-chat product activation or UI/API surface.
