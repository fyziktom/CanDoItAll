# Agent module continuation roadmap

Prepared 2026-09-07 after [Capabilities-03 closure](../../UI_AgentCapabilities_03_Extraction_Sandbox_Bundle/closure.md). This is a forward plan; historical Agents SB01-SB09 and provider/capability proof retain their observations. No item after Capabilities-03 is implemented or implicitly authorized by this roadmap.

| Order | Bounded next work | Prerequisite / decision |
|---|---|---|
| 1 | Capabilities-03 real extraction, existing sandbox, direct-watch comparison | Closed in current run. Semantic/physical/sandbox ready; measurements limited to frozen environment/edits. General capability CRUD and details-load fallback remain separate. |
| 2 | [Agents Overview program](../../UI_AgentsOverview_01_State_Read_Seams_Bundle/README.md), CDA-UI-SEAMS-AGENTS-OVERVIEW-01 | Prepared only: O00 inventory/RED, O01 state/reads/presentation, O02 owned effects/composition, O03 fresh baseline/extraction/sandbox/watch. Separate execution instruction required. |
| 3 | Governance list/detail accepted state, read lifetime and extraction | Inventory current query/selection/lifetimes first; preserve authority. Own bounded children and baseline, not a copy of Overview decisions. |
| 4 | Diagnostics explicit state/error/cancellation, then extraction | Separate observation from commands/effects; never replay diagnostics through a read retry. Current source must determine actual boundaries. |
| 5 | Agent Details multi-child program | Preserve and regression-test existing fail-closed core-load hardening; separately inventory remaining save/delete/global capability CRUD outcomes, first-create/recovery identity, technical child ownership, then extraction/sandbox. Do not claim fail-closed is wholly missing or reopen accepted behavior without evidence. |
| 6 | Voice and Floating Chat settings slices | Bound editor/read/save/effect ownership separately; actual subtree/runtime dependencies determine extraction eligibility. |
| 7 | Agent Chat multi-child program | Bootstrap/accepted selection -> sessions/messages -> send/approval execution lifetimes -> attachments/voice -> floating/full-page reuse -> extraction/sandbox. No one giant chat-controller bundle. |
| 8 | Simple Chats within existing AgentFramework.Llm.SimpleChats stack | Inspect its own seams; keep feature rendering/composition in its dedicated stack. Do not move Simple Chats into generic AgentFramework.UI. |

Every extraction follows proved in-place seams, a real rendered dependency/asset closure, representative sandbox and pre/post direct-watch protocol. Production route/bookmarkability design stays a distinct owner decision. Generalization goes to the shared architecture bundle only after implementation proves it. No sibling work, branch cleanup or repository merge-readiness claim is authorized here.
