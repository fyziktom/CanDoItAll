# Runtime execution report

## Overall status

- Execution: `B07 implemented and locally proven — hosted three-platform aggregate deferred`
- Active subbundle: `B07 Runtime three-platform CI, E2E, and final closure — hosted execution pending`
- Final gate: `R4 deferred by operator instruction`

## Subbundle progression

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
|---|---|---|---|---|---|
| B00 | C4 or provisional handoff | R0 | Provisional core handoff accepted; inventory and cross-host characterization complete | Completed — R0 GO | Hosted/macOS proof remains deferred under `HOSTED-PORTABILITY-VALIDATION-001`; no support claim. |
| B01 | R0 | R1a | Gate R0 GO; implementation, governed proof, and independent review complete | Completed — Gate R1a GO | Windows 133/133 + 2/2 and Linux 133/133 + 2/2 are green, including typed long-running ownership and lease recovery. Actual macOS remains deferred under `RUNTIME-MACOS-VALIDATION-001`. |
| B02 | R1a | Workbench gate | Original independent review recorded NO-GO; all four blockers plus the interrupted-shutdown re-review defect were remediated and independently accepted | Completed — Workbench gate GO | Windows/Linux 98/98, component 24/24, default/headless Playwright 1/1 each, three affected builds green; reviews 11–13. |
| B03 | Workbench gate | R2 | Windows/Linux 139/139 unit and 11/11 integration, including actual Linux parent-exit/recovery; affected builds/startup/scan green; reviews 14–16 | Completed — Gate R2 GO | B04 only; actual macOS remains deferred. |
| B04 | R2 | R3a | Windows/Linux 154/154 unit and 18/18 integration; ten clean builds; governed source/artifact proof and independent review complete | Completed — Gate R3a GO | Actual macOS remains deferred. |
| B05 | R3a | R3b | Five independent-review blockers remediated; bounded re-review accepted 15 corrections, 16 assertions, 29 source and 25 artifact hashes, and both-host focused proof | Completed — Gate R3b GO | Actual macOS and NuGet publication remain explicitly deferred. |
| B06 | R3b | R3 | Exact 124-method Windows/Linux proof, governed hashes, architecture/receipt/source guards, and independent review complete | Completed — Gate R3 GO | Actual macOS remains explicitly deferred. |
| B07 | R3 | R4 | Active matrix and exact no-build runner implemented; Windows 422+33+1 and Linux 422+33 locally green | Implemented locally — R4 deferred | Hosted Windows/Ubuntu/macOS artifacts and independent Final Gate R4 review remain deferred. |

## Runtime ownership evidence

| Surface | Plan owner | Execution owner | Lifecycle/registry | Recovery | Domain semantics | Result |
|---|---|---|---|---|---|---|
| Workbench runtime node | Workbench | Typed compiler plus B01 direct-execution adapter | Host-lifetime Workbench registry retains exact B01 sessions; exact-node stop and cancellation-safe shutdown cleanup | New scoped UI/application adapters recover the same host-lifetime registry; no durable cross-restart claim | Workbench | Completed — B02 Workbench gate GO |
| Manager supervisor | Manager | B01 canonical process host through Manager coordinator | Durable non-secret registry | Windows WMI/Linux proc/macOS invariant ps bounded leaves | Manager | Completed B03 — Gate R2 GO; actual macOS deferred |
| MCP local stdio | MCP | Canonical B01 duplex process host through the B04 adapter | MCP-owned acquired session with bounded cleanup | Exact owned-session cleanup; residual process fails explicitly | MCP | Completed B04 — Gate R3a GO |
| External process tool | Tool capability | Canonical workspace host through module adapter | Invocation only | None | Tool capability | B01 implemented; B04 retains capability policy |
| Docker plugin | Docker plugin | B01 canonical host through scoped Docker adapter | Invocation only | None | Plugin recipe | Completed B05 — Gate R3b GO |
| Process strategy | Processes | Capability descriptors | Processes runtime/persistence | Processes | Processes | Authoritative B06 boundary |

## Actual-host evidence

| OS/profile | Process primitive | Workbench | Manager | MCP/tools | Plugins/FileTools | Processes | Result |
|---|---|---|---|---|---|---|---|
| Windows | 165/165 named unit | Launcher contracts included | 4/4 Integration plus unit contracts | MCP/tool contracts included | Plugin contracts included | Driver contracts included | B00 characterization green |
| Ubuntu headless/interactive | 165/165 named unit in Linux SDK container | Launcher contracts included | 4/4 Integration plus unit contracts | MCP/tool contracts included | Plugin contracts included | Driver contracts included | B00 headless characterization green |
| Windows B01 focused | 133/133 named unit/lifecycle + 2/2 actual process integration | Not in B01 | Not in B01 | External/Git adapters included | Canonical primitive and typed session/lease contract included | Not in B01 | B01 implementation green |
| Linux B01 focused | 133/133 named unit/lifecycle + 2/2 actual process integration | Not in B01 | Not in B01 | External/Git adapters included | Canonical primitive and typed session/lease contract included | Not in B01 | B01 implementation green |
| Windows B03 focused | B01 host consumed through Manager coordinator | Not in B03 | 139/139 unit/lifecycle + 11/11 ManagerPortability integration | Not in B03 | Not in B03 | Not in B03 | B03 implementation green |
| Linux B03 focused | B01 host consumed through Manager coordinator | Not in B03 | 139/139 unit/lifecycle + 11/11 ManagerPortability integration, including parent-exit/recovery | Not in B03 | Not in B03 | Not in B03 | B03 implementation green |
| Windows B04 focused | B01 host consumed through MCP/tool adapters | Not in B04 | Not in B04 | 154/154 unit + 18/18 integration | Not in B04 | Not in B04 | B04 implementation green |
| Linux B04 focused | B01 host consumed through MCP/tool adapters | Not in B04 | Not in B04 | 154/154 unit + 18/18 integration | Not in B04 | Not in B04 | B04 implementation green |
| Windows B06 focused | B01–B05 typed owner ports consumed | Workbench capability facts retained | Manager capability facts retained | MCP/tool capability facts retained | Plugin/FileTools/Docker capability facts retained | 206/206 exact regression cases + 1/1 integration | B06 implementation green — Gate R3 GO |
| Linux B06 focused | B01–B05 typed owner ports consumed | Headless/unavailable behavior retained | Linux capability facts retained | Local/remote MCP and tool facts retained | Linux/headless capability facts retained | 206/206 exact regression cases + 1/1 integration | B06 implementation green — Gate R3 GO |
| Windows B07 focused | Canonical B01 host used | Runtime actions + approval + missing dependency + foreign path browser proof | Lifecycle/recovery category included | MCP/external-tool category included | Plugin/FileTools category included | Process capability category included | 422/422 unit + 33/33 integration + 1/1 browser |
| Linux B07 focused | Canonical B01 host used | Headless/runtime contracts included; browser deferred locally | Linux lifecycle/recovery category included | MCP/external-tool category included | Linux/headless plugin category included | Process capability category included | 422/422 unit + 33/33 integration; hosted browser pending |
| macOS headless/interactive | | | | | | | Not started |

## Browser validation analytics

| Subbundle | Route | Viewport | Capability fixture | Playwright evidence | Screenshots | Result |
|---|---|---|---|---|---|---|
| B02 | Typed direct execution | Explicit PowerShell/POSIX modes with enforced per-launch approval | Windows-only `runas`; Unix/macOS unsupported | Host-lifetime Workbench registry retains B01 owned sessions | Core workspace/external-target authority; agent projection redacted | Available/dependency/headless/foreign screenshots | Completed — Workbench gate GO |
| B07 | `/projects` to ordinary Project Structure navigation | 1600×1000 | Direct, explicit-script approval, missing executable, optional headless terminal, foreign path | `runtime-portability-browser.trx` 1/1 | `output/playwright/b07-runtime-capabilities-*.png` | Windows local green; hosted Ubuntu/macOS pending |

## Raw request closure

| Raw note | Status | Proof |
|---|---|---|
| Tools/runtime nodes/processes after core foundations | Implemented locally | Gate R0, Gate R1a, B02 Workbench gate, Gate R2, Gate R3a, Gate R3b, and Gate R3 GO; B07 Windows/Linux local proof green |
| Refactor first when required | Planned with approved boundary map | B00 ownership/split gate; B01 foundation |
| Consider a separate bundle | Solved in preparation | This bundle |
| Special tools/domain drivers included | Completed locally | B05 and B06 completed through Gate R3; actual macOS aggregate remains B07-deferred |
