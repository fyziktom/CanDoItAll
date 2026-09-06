# Requirements and input closure contract

| ID | Required outcome | Owner/proof |
|---|---|---|
| C01 | Production uses one extracted rendering implementation; no copied sandbox-only panel/card. | SB01 direct RCL/consumer builds, dependency graph, existing controlled rendering/intent tests. |
| C02 | Host, dialog/service/chat/persistence effects remain in module; same state/intent semantics. | SB01 production host tests and browser smoke. |
| C03 | Real card, mapper, Conversations card, avatars, TreeView, tooltips and CSS isolation render. | SB02 inspected normal/tooltip/tree/empty/loading screenshots, network and computed styles. |
| C04 | Independent browser host has no full module/Core/runtime/service graph. | SB02 evaluated dependency closure, actual standalone startup without backend/database services. |
| C05 | Live sibling source mode, revisions, SDK and Tailwind pipeline match both comparisons. | SB00/SB03 machine/graph/asset manifest; package substitution forbidden. |
| C06 | Cold startup measured separately from warm edits. | SB03 protocol and separate cold.csv. |
| C07 | At least three distinct supported edits each in Razor, C# and CSS, each repeated at least three times per host. | SB03 complete 81-or-more primary warm trial ledger across pre-extraction app, post-extraction app and sandbox including failures and undo stabilization. |
| C08 | Min/range/median plus mechanism classification and reproducible evidence. | SB03 raw trial ledger, commands/source hashes/visible predicate and timestamp contract. |
| C09 | No unsupported performance claim, discarded slow trials or incomparable asset setup. | Final independent reread/measurement validity gate. |
| C10 | No provider/history/route/full-editor extraction, visual redesign or sibling refactor. | Final diff/consumer review. |

All rows start Planned. Execution closure marks Solved/Partially solved/Not solved with actual artifacts; preparation is not measurement evidence. Existing historical documentation-log debt is tracked separately and prevents an unconditional repository merge claim.

## Executed outcome map

| Requirement | Outcome | Evidence |
|---|---|---|
| C01 | Solved | SB01 closure and source parity |
| C02 | Solved | SB01 host tests/browser; SB03 provider-preservation receipt |
| C03 | Solved | SB02 browser/asset/tooltip acceptance |
| C04 | Solved | SB02 restore closure and independent SourceWatch startup |
| C05 | Solved | SB00 machine/assets; SB03 re-entry and source restoration |
| C06 | Solved | SB03 cold.csv and nine process-cold ledger records |
| C07 | Solved | SB03 complete 81-trial primary warm matrix |
| C08 | Solved | SB03 results.md, CSV, ledger and SDK appendix |
| C09 | Solved | SB03 explicit no-general-warm-speedup verdict and retained exclusions/outliers |
| C10 | Solved | SB03 source audit; all 28 provider production hashes preserved |

Solved means the stated boundary/evidence requirement was met. It does not convert the observed Razor/C# timings into an improvement or waive historical repository documentation debt.
