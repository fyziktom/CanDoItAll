# Bundle status

Prepared: 2026-08-24  
Overall state: `READY_FOR_SB00`

| ID | State | Depends on | Progression owner |
| --- | --- | --- | --- |
| SB00 | `READY` | none | baseline and architecture decision lock |
| SB01 | `LOCKED` | SB00 | protocol, identity, access context |
| SB02 | `LOCKED` | SB01 | persistence and reconciliation model |
| SB03 | `LOCKED` | SB02 | central catalog API |
| SB04 | `LOCKED` | SB03 | bounded OpenAI-compatible relay |
| SB05 | `LOCKED` | SB03, SB02 | source sync and imports |
| SB06 | `LOCKED` | SB04, SB05 | local runtime projection and hybrid use |
| SB07 | `LOCKED` | SB06 | backend checkpoint and three-instance proof |
| SB08 | `LOCKED` | SB07 | desktop management UI |
| SB09 | `LOCKED` | SB08 | component and browser proof |
| SB10 | `LOCKED` | SB09 | operator docs and repeatable E2E tooling |
| SB11 | `LOCKED` | SB10 | OpenAPI export and SharedInfo skills |
| SB12 | `LOCKED` | SB11 | final regression, running stack, closure |

## State rules

- Only one subbundle may be `READY` or `IN_PROGRESS`.
- `DONE` requires a passing proof manifest and completed handoff.
- `BLOCKED` requires the exact missing authority or external state.
- A failed progression gate leaves downstream work locked.
- Any named reopen trigger may move an earlier subbundle back to `READY_FOR_REVIEW`.
