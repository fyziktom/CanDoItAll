# CP2 — Core conversation extraction review

## Listing and threads

- [x] neutral participant and thread owners have direct tests
- [x] Agent facades preserve mapping and callbacks
- [x] agent-only semantics remain adapter-owned
- [x] opaque keys remain stable
- [x] selector/accessibility behavior is preserved

## Workspace

- [x] transcript, message, markdown, and composer are neutral
- [x] HTML remains disabled in markdown
- [x] hidden-context parsing remains Agent-owned
- [x] execution, approvals, voice, attachments, prompt gallery, and runtime details remain Agent-owned slots
- [x] current callbacks and cancellation behavior are unchanged
- [x] focus, scroll, copy, timestamps, token metadata, and overlays pass focused proof

## Architecture

- [x] old owners lost real presentation responsibility
- [x] no new partial-file growth
- [x] no facade-only extraction
- [x] impacted tests and browser proof pass

## Decision

- [x] pass to SB06
- [ ] reopen SB03/SB04/SB05
- [ ] repair architecture

Evidence: `proof/SB05/architecture-change-record.md`, `proof/SB05/manifest.json`, and `proof/SB05/browser-parity.md`.
