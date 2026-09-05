# Behavior preservation and isolation matrix

This is the acceptance contract, not test execution evidence. **P** = preserve observed behavior; **S** = safeguard necessary for new ownership/lifetimes; **U** = ambiguous existing behavior requiring characterization before dependent edits. SB01 maps each row to existing exact test methods/data cases or a named characterization scenario, records the observed oracle, and identifies gaps. The owning phase freezes added/replacement names before source changes.

A row may require multiple cases (success, failure, absent data, overlap). Do not use one happy-path test or an unchanged total to close a row. New safeguards must not silently change unrelated user behavior.

| ID / kind | Scenario and required oracle | Owner / proof |
|---|---|---|
| B01 / P | Current route parse/build/defaults, unknown/obsolete tabs, agent/team and Simple Chat/usage context stay compatible | SB01/SB02; route tests + page integration |
| B02 / P | Providers and RequestHistory hosts perform no overview/usage aggregate or history read until requested; first Overview transition performs one intended load | SB02; existing two history-host cases with recording query fakes |
| B03 / P | Overview metrics, HR/managed-agent/avatar/bound-resource counts and usage selection retain independent triggers/loading/errors | SB02; real query operation + page composition |
| B04 / P | Selected agent/team, accessible context, provider/agent collections, and context readiness reach AgentChatContextSurfaceProvider before chat actions | SB02/SB03; host interaction and context regression |
| B05 / P | Catalog initial data, SkipCatalogRepair, explicit repair/reload, empty/error/retry, immediate local search and expansion behavior remain equivalent | SB03; real operations + controlled rendering |
| B06 / P | Select, double-click/open, requested ID open once, subsequent rerenders, changed requested ID, missing ID and close/reopen remain distinct | SB01/SB03; real page/catalog/dialog host sequence |
| B07 / P | Exact Hr/PromptsCurator/WorkflowCurator/Scheduler identities drive managed behavior, protection, and chat; lookalike names/tags do not | SB03/SB05; identity policy + UI/application operations |
| B08 / P | Create/edit/delete team and member dialog results refresh/reconcile once, preserving each current confirmation policy and selection | SB03; dialog-host flows + real catalog operations |
| B09 / P | Editor loads existing/new model, supplied providers, all references and defaults; first section Identity and all ten labels/order preserved | SB04; real component with fake loading boundary |
| B10 / P | Provider and secret load failures remain independent partial errors; recovery retains the current draft | SB04/SB05; parameterized failure/retry interactions |
| B11 / P | Project list remains explicit/lazy with independent loading/error/retry and existing selection semantics | SB04/SB05; unopened-section call counts + real child interaction |
| B12 / U | Core agent/capability failure: baseline catches errors and clears loading; establish rendered state and whether saving remains possible. Do not silently replace it with a claimed existing fail-closed policy | SB01 before SB04; characterization; separate defect/scope decision if unsafe |
| B13 / P | Normal save normalization, permission flags, defaults, tags, aliases, model/runtime/image/workspace settings and selected references round-trip; editor stays open and Saved/caller refresh are preserved | SB05; pure policy + actual save boundary + host |
| B14 / P | Capability toggle/wizard: existing agent saves the whole current draft; new draft stages assignment; wizard external creation and agent assignment outcomes remain distinct | SB05; record exact mutation calls, failures and final draft |
| B15 / P | Managed agent delete is blocked; ordinary delete confirmation/cancel/failure/success retain behavior; DialogReference result or Saved delivers exactly once | SB05; real host and operation failure tests |
| B16 / P + S | Clear creates blank draft; synchronize create target/session while catalog selection remains independent. First save binds returned identity/version; later save updates that agent | SB04/SB05; host/session transitions + command tests |
| B17 / P + S | Carry ExpectedUpdatedAtUtc from load through save; handle conflict without force-overwrite; successful refresh supplies current token without losing later edits | SB05; actual use case/adapter with version conflict |
| B18 / P + S | Persistence failure leaves recoverable draft; committed write plus refresh or callback failure is distinguishable and never blindly replays the mutation | SB05; failures at each boundary, outcomes and call counts |
| B19 / S | Same-target section changes retain session/edit context; a new target/load/reset/disposal invalidates old completion; concurrent editor instances never share mutable draft | SB04/SB05; delayed success/failure tests + instance isolation |
| B20 / P + S | Close/cancel/target change during pending work preserves characterized UX; ignore stale UI publication while acknowledging any already committed write. Cancellation is not rollback | SB04/SB05; host lifecycle + operations |
| B21 / P | Runtime/provider model/thinking effort, approval settings, image and voice fields retain supported choices, enablement, normalization and saved values | SB04/SB05; real sections, existing settings tests + round-trip |
| B22 / P | Workspace roots and storage catalog: saved selections, provider/root validation, add/edit/remove, nested dialog and retry use real descendants with fake external boundaries | SB04/SB05; subtree scenario tests |
| B23 / P | Project/secret/process permissions and selected identities stay scoped; unavailable references remain explicit and no secret values enter UI snapshots | SB04/SB05; settings + operation mapping |
| B24 / P | Avatar picker opens real nested UI, loads available choices, generates via existing gateway, and handles success/failure without unrelated draft loss | SB04/SB05; existing avatar tests + child interaction |
| B25 / P | Shared provider refresh uses actual button flow, refreshes sources/models, and retains unsaved draft selection/fields or the current explicit invalid-selection behavior | SB04/SB05; real child/service fake and host notification |
| B26 / P | Memory profiles/drivers, alias bindings, invocation mode and errors retain current behavior; opening real Memory section has fully registered fakeable external dependencies | SB04/SB05; real Memory child and stored settings |
| B27 / P | Workflows shell button navigates through public UI; unrelated provider/voice/governance/Simple Chat panes still compose with unchanged inputs | SB02/SB06/SB07; exact adjacent case + selected host smoke |
| B28 / P | Normal close/escape/confirmation, overlay stacking, sticky actions, internal scrolling and notifications remain usable at large desktop viewport | SB04/SB07; real browser interactions/screenshots |
| B29 / P | Actual UI registration resolves production operations normally and exercises read/mutation/result mapping without private state injection or uninitialized service objects | SB02–SB07; composition/integration proof |
| B30 / S | Sandbox candidate's entire selected subtree/type/reference/asset graph is inventoried; fake render claims and measured standalone-watch claims remain separate | SB01/SB03/SB07; inventory, measurement baseline and handoff |

For B18/B20, characterize current notification/result ordering and persistence boundaries first. Adding explicit operation outcomes is an isolation safeguard, not permission to redesign messages or transactional semantics. An indeterminate write outcome must not be labeled a proven failed write.

Existing broad end-to-end or source-shape tests need not be retained verbatim, but every legitimate behavior they protect must have a destination. A gap blocks the affected phase; document an independent existing defect without mixing its repair into the refactor.
