# Phase Plan

## Execution Order

1. SB01 authoritative catalog/pricing refresh and kind-change isolation.
2. SB02 rebuild both apps, UI setup against real providers, parity/execution/usage proof.
3. SB03 repair normal-browser Simple Chats access and revalidate both deployed hosts.
4. SB04 consistent avatars, rebuilt pair and isolated manual-setup third client.
5. SB05 compact provider controls and modal shared connections.
6. SB06 scope selection, durable token administration and recoverably reset 5214.
7. SB07 typed shared thinking, per-agent enforcement and real main-model suggestions.
8. SB08 final real multi-agent proof and preserved three-container handoff.
9. SB09 provider model thinking settings and explicit stale import recovery.
10. SB10 real UI proof on source and both clients, with all data preserved.

## Subbundle Dependency Map

```mermaid
flowchart LR
  SB01["SB01 catalog authority"] --> SB02["SB02 real two-instance proof"]
  SB02 --> SB03["SB03 normal-browser access and API boundary"]
  SB03 --> SB04["SB04 avatar and fresh-client handoff"]
  SB04 --> SB05["SB05 compact provider administration"]
  SB05 --> SB06["SB06 managed tokens and fresh handoff"]
  SB06 --> SB07["SB07 shared thinking and suggestions"]
  SB07 --> SB08["SB08 actual per-agent upstream proof"]
  SB08 --> SB09["SB09 model thinking settings and stale import recovery"]
  SB09 --> SB10["SB10 source and both-client UI proof"]
  Real["Reachable real upstreams"] --> SB02
```

## Critical Subbundles

- SB01 is the critical foundation, Proof tier: Governed. Required downstream check:
save/reload source, publish/synchronize, assert exact client catalog and prices.
SB02 Proof tier: Governed because prior acceptance was disputed.
No full-suite gate. Dependency criticality does not broaden test scope.
SB03 Proof tier: Governed. N005 reopens normal-browser handoff, not catalog correctness.

## Phase Gates

- SB09 frozen checkpoint: CodeAnalytics TIA3001/TIA3004 cannot resolve actual changed
  members across reference-backed test workspaces; required AllSuppliedSuites is honored
  with one broad Unit/Components/Integration run. Focused discovery is 138 Unit and 35
  Components plus 56 Integration, all passing. UI acceptance and broad review are
  complete. Unit: 7037/1, Components: 1110/52, Integration: 1133/10 pass/fail plus
  one opt-in skip. All failed identities occur in SB07, with reviewed unchanged causes.
  Final layout-only invalidation has
  35 passing component cases and repeated final-image UI plus Sol High inference.

- Prepared validator and architecture entry review before production edits.
- SB01 targeted discovery/tests/build and semantic proof before SB02 deployment.
- SB02 actual UI and provider evidence before final closure.
- Ollama-unavailable cases remain Blocked; other in-scope verification proceeds.
- Freeze test selection from --list-tests output before each execution. Zero fails.
- Reopen SB01 for catalog/price injection after refresh or round-trip; revalidate SB02.
- SB05 has 11 focused passing component cases and actual desktop MCP interaction proof.
- SB06 focused tests and live same-token HTTP denial pass. CodeAnalytics could not resolve
  all dispatch and requested both supplied Unit/Integration suites; these were expanded
  beyond the prepared bounded scope. Both runs completed. Record unchanged pricing/seed/relay fixture failures,
  rather than changing catalog fixtures or claiming a clean full suite.
- SB07/SB08 completed: exact final discovery 206 Unit/46 Components/56 Integration,
  nine real upstream requests with complete source usage and final same-image health.
  CodeAnalytics public-contract/dynamic-dispatch invalidation required one broad run
  of each supplied suite at the SB07 frozen checkpoint; failures are classified in
  proof/SB07/broad-regression-results.md. Later temperature/envelope/terminal changes
  used bounded focused invalidations and real final-image requests, not repeated broad gates.

## UI Target Policy

1920x1080 desktop. Preserve existing split provider list/editor, tabs and agent/chat dialogs.
Main editor or dialog body owns vertical scroll; price table owns horizontal scroll.
No mobile tuning or shared BaseLib changes.
