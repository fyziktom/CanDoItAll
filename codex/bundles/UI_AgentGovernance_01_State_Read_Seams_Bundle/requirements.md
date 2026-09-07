# Normalized requirements

All requirements originate in Phase C of [the preserved owner instruction](inputs/01-owner-authorization.md). Earlier Phase A/B instructions are prerequisite context, not an invitation to rerun their closed work.

| ID | Required result | Owner |
|---|---|---|
| GOV-01 | Rediscover post-Overview source, remote/index/siblings, actual graph, assets and tests; preserve historical closure. | Preparation and G00 |
| GOV-02 | Preserve valid PreferredAgentId echo, explicit All agents, missing requested agent fail-closed and current sorting/Take behavior. | G00-G01 |
| GOV-03 | Capture agent target, selected run and request ownership before await; cancel/fence late success, failure, callback and finally on replacement/disposal. | G01 |
| GOV-04 | Accepted run list and detail have independent state/retry; retain useful same-target stale data honestly; never show another target's data. | G01 |
| GOV-05 | Manual run choice supersedes refresh detail selection; wrong run/agent payload cannot be accepted. | G01 |
| GOV-06 | Only accepted agent target publishes SelectedAgentChanged/context access. Detail failure cannot invalidate a known agent. | G01-G02 |
| GOV-07 | Real outcomes, summaries, approvals, artifacts, checkpoints, tool receipts, timeline and metrics render through a controlled service-free surface. | G02 |
| GOV-08 | Bounded public errors; no raw exception, serialized session, approval arguments or new operational payload exposure. | G01-G03 |
| GOV-09 | Page/host/read/session effects remain Module; no generic controller, event bus, new contract project or dependency inversion through Razor. | G01-G03 |
| GOV-10 | G03 fresh full-app baseline precedes movement; move real pure children for both consumers into existing UI; actual graph/assets must pass. | G03 |
| GOV-11 | Extend existing sandbox only, preserve Catalog/Capabilities/Overview defaults and Parity/Fast assets; deterministic safe fixtures, no production services. | G03 |
| GOV-12 | Same-machine direct-watch evidence with three cold runs and nine supported edits repeated three times per host; retain failures/outliers/restoration. | G03 |
| GOV-13 | Exact public tests, registered persistence/read integration, real browsers and unweakened static/evidence gates. | Every executing child |
| GOV-14 | This preparation changes documents only. No Governance implementation or routing/other hotspot work. | Preparation |

Product decisions for future implementation: validated agent selection owns chat access; list/detail availability is separately visible. An unknown agent stays an explicit requested target with Failed access. All agents is a deliberate null selection, never a fallback for an invalid request. A missing run is a safe unavailable detail with usable accepted list, not a reason to silently choose a different row. Same-agent list refresh preserves the newest manual selection if still present. If that row disappears from an accepted refreshed list, keep its identity in an explicit unavailable detail until the user chooses another run; initial load alone may choose the first returned run.

Do not add approval decisions, run continuation/cancellation commands, diagnostics, provider/history refactors, production query keys, routed dialogs, durable mutation recovery or backend audit changes. Preserve existing badge-tone precedence and text/sorting unless a separate direct defect justifies a narrow correction. No sibling edits are planned.

Execution additions under the new authorization: GOV-15 explicit invariant UTC/offset/DST/two-culture/null timestamp proof; GOV-16 immutable allowlist and forbidden payload/sentinel absence; GOV-17 one lightweight shared timeline/metric implementation and both actual consumers; GOV-18 delayed token registration and late-completion cancellation proof. GOV-14 remains the historical preparation constraint, not a prohibition on now-authorized G00-G03.
