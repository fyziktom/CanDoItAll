# CP4 — Architecture and consumer closure

## Consumers

- [x] AgentChatPanel
- [x] FloatingAgentChatHost
- [x] AgentCatalogPanel
- [x] AgentSwitchDialog
- [x] AgentDetailsDialog
- [x] ContextualAgentWorkspaceWindows
- [x] ProcessWorkspaceShell
- [x] every additional live reference discovered in SB01

## Architecture

- [x] before/after project graph
- [x] no cycles or wrong direction
- [x] no forbidden neutral dependency
- [x] no duplicate presentation implementation
- [x] compatibility facades are thin and purposeful
- [x] no new partial expansion
- [x] neutral behavior has independent tests
- [x] architecture review gate passes

## Phase exclusions

- [x] no LlmChats production UI reference
- [x] no mixed catalog/filter
- [x] no context capture
- [x] no API/SSE client
- [x] no backend changes

## Decision

- [x] pass to SB09
- [ ] reopen an implementation subbundle
- [ ] repair bundle

Evidence: `proof/SB08/manifest.md`, `proof/SB08/consumer-migration.md`, `proof/SB08/architecture-review.md`, fresh cross-consumer 81/81, and the unchanged analyzer-required Components 990/990 run.
