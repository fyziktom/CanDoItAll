# Actual Agents dialog ownership audit

| Surface/effect | Owner and same-page behavior | Departure/close result | Evidence |
|---|---|---|---|
| Three Overview usage dialogs | Page dialog group; retained across equivalent route echo | Scope replacement or leaving Overview cancels only this group | Public lifecycle tests; real Web pending/dialog/tab cases |
| Defaults confirmation | Page lifetime; same reference survives query/fragment | Actual page departure closes it; normal explicit confirmation still executes once | Real Web Back/Forward, fragment, completion and departure |
| Agent editor | Catalog host token; presentation target remains editor/session-owned | Catalog removal cancels editor; nested delete/setup/auto-approval use editor token | Existing owning tests and source call-site inspection |
| Team editor | Catalog host owns shell; editor now owns read/save/nested picker | Closing editor or removing Catalog cancels picker and suppresses late UI effects | Failing-first public real DialogHost test and Web team navigation |
| Team membership | Controlled selection dialog, no persistence inside dialog | Catalog token closes it; host checks generation before membership write/reload | Source inspection and stable owning coverage |
| Capability details/setup | Capability host token passed to shell and child owner parameter | Host disposal cancels owned work; current results alone refresh catalog | Existing cancellation/source ownership contract, unchanged |
| Provider/shared source overlays | Rendered modal children, provider session and overlay lifetimes | Component removal disposes their existing operation owners | Source inspection; lease does not change rendered child ownership |
| Agent delete/setup/auto-approval | Per-editor session token on each nested OpenAsync | Session replacement/disposal removes only its children | Existing public nested ownership tests, unchanged |
| Independent same-route overlays | Own stable presentation/target or caller lifetime | Explicit close/cancellation unchanged; path departure closes all via default service path rule | Direct multiple-lease/unrelated-reference tests |
| Chat/runtime/history overlays | Existing independent chat/runtime presentation; not re-owned by Overview | Page navigation retains default service departure behavior | Call-site audit only; no Chat/Voice redesign or new behavioral proof claimed |

The lease is a page-level opt-in to preservation, not a transfer of dialog lifetime to the page. Each effect owner still cancels its own children when its component or target ends. The only newly demonstrated owner defect was the team's unowned icon picker; no generic routing/dialog policy framework is introduced.
