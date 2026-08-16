# CP2 — Core conversation extraction review

## Listing and threads

- [ ] neutral participant and thread owners have direct tests
- [ ] Agent facades preserve mapping and callbacks
- [ ] agent-only semantics remain adapter-owned
- [ ] opaque keys remain stable
- [ ] selector/accessibility behavior is preserved

## Workspace

- [ ] transcript, message, markdown, and composer are neutral
- [ ] HTML remains disabled in markdown
- [ ] hidden-context parsing remains Agent-owned
- [ ] execution, approvals, voice, attachments, prompt gallery, and runtime details remain Agent-owned slots
- [ ] current callbacks and cancellation behavior are unchanged
- [ ] focus, scroll, copy, timestamps, token metadata, and overlays pass focused proof

## Architecture

- [ ] old owners lost real presentation responsibility
- [ ] no new partial-file growth
- [ ] no facade-only extraction
- [ ] impacted tests and browser proof pass

## Decision

- [ ] pass to SB06
- [ ] reopen SB03/SB04/SB05
- [ ] repair architecture
