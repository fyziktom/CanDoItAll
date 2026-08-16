# CP4 — Architecture and consumer closure

## Consumers

- [ ] AgentChatPanel
- [ ] FloatingAgentChatHost
- [ ] AgentCatalogPanel
- [ ] AgentSwitchDialog
- [ ] AgentDetailsDialog
- [ ] ContextualAgentWorkspaceWindows
- [ ] ProcessWorkspaceShell
- [ ] every additional live reference discovered in SB01

## Architecture

- [ ] before/after project graph
- [ ] no cycles or wrong direction
- [ ] no forbidden neutral dependency
- [ ] no duplicate presentation implementation
- [ ] compatibility facades are thin and purposeful
- [ ] no new partial expansion
- [ ] neutral behavior has independent tests
- [ ] architecture review gate passes

## Phase exclusions

- [ ] no LlmChats production UI reference
- [ ] no mixed catalog/filter
- [ ] no context capture
- [ ] no API/SSE client
- [ ] no backend changes

## Decision

- [ ] pass to SB09
- [ ] reopen an implementation subbundle
- [ ] repair bundle
