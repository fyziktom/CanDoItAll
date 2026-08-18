# Change control

## Baseline movement

Reviewed heads are in `manifest.json`. SB00 must replace them with the actual execution heads. If
`development` advances after CP0 across affected persistence, provider, API, migration, or streaming
surfaces, reopen SB00 and dependent proof.

## Separate-scope work

The following require a separate bundle decision:

- Razor/shared-component/floating-chat work;
- contextual inputs, Project Structure, attachments, voice, memory, or tools;
- public chatbot deployment, participants, moderation, channels, or human handoff;
- WebSockets or a third-party broker;
- replacing PostgreSQL or the provider runtime.

## Architecture changes

Any new project/reference, transaction owner, state, event kind, or provider capability must update
architecture records, specifications, target map, focused tests, traceability, and proof.

## Invalidation

- Reopening SB01-SB05 invalidates CP1 and all downstream streaming/API proof.
- Reopening SB07-SB10 invalidates CP2 and FINAL proof.
- Any source/migration change after SB13 invalidates FINAL.
