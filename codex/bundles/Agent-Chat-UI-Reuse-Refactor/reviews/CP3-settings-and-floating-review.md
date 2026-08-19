# CP3 — Settings and floating review

## Settings

- [x] identity/avatar/instructions fields are reusable and source-neutral
- [x] provider/model presentation uses neutral options
- [x] current Agent save/load/version behavior remains in AgentDetailsDialog
- [x] Agent-only tabs and policies remain Agent-owned
- [x] floating lifecycle fields are separated from prepared-Agent semantics

## Floating UI

- [x] presentation seam exists
- [x] host remains Agent-only
- [x] no mixed catalog/filter/tab
- [x] no Simple Chat context button
- [x] context, affinity, history, handles, preparation, retention, close/hide/stop behavior remain unchanged
- [x] normal and open-overlay desktop proof is inspected

## Decision

- [x] pass to SB08
- [ ] reopen SB06/SB07
- [ ] repair architecture

Evidence: `proof/SB06/browser-parity.md`, `proof/SB07/architecture-change-record.md`, `proof/SB07/browser-parity.md`, focused 9/9 tests, and required Components 990/990.
