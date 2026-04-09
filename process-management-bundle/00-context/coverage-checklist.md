# Coverage checklist

| ID | Discussed requirement | Capture status |
|---|---|---|
| C01 | Process management must come before the intelligence lake | Captured in ADR-PROC-010 and delivery waves. |
| C02 | Use the correct CanDoItAll repo and the AgentFramework overlay as the implementation baseline | Captured in source-note, repo-fit-analysis, and convergence review. |
| C03 | Processes should be added directly into CanDoItAll | Captured by PRM-F01 and target module topology. |
| C04 | Agent creation should remain in CRM-HR | Captured by PRM-F03 and ADR-PROC-003. |
| C05 | Process modeling needs interactive mindmap/flow-diagram style canvas | Captured by PRM-F09 and the canvas workbook. |
| C06 | Handoffs need explicit responsibility, actor order, and governance | Captured by PRM-F05, PRM-F06, and PRM-F07. |
| C07 | AgentFramework should be considered now but integrated later | Captured by PRM-F13 and ADR-PROC-006. |
| C08 | SQLite must remain viable for users, with PostgreSQL as scale-up path | Captured by PRM-F15 and ADR-PROC-002. |
| C09 | Training signals should exist, but training-only data must not pollute normal execution | Captured by PRM-F14 and deferred seams. |
| C10 | Shared projects should keep ownership of their own processes | Captured by PRM-F10 and shared-project navigation rules. |
| C11 | When defining a process role, users should be able to select it from reusable manager-defined templates | Captured by PRM-F16, ADR-PROC-012, ADR-PROC-013, and the staffing model workbook. |
| C12 | Managers should define role templates and hand them to HR for sourcing suitable people or agents | Captured by PRM-F16 and the CRM-HR workforce/recruiting/assignments seam. |
| C13 | AI agents should follow a similar template-and-fulfillment pattern as human roles | Captured by PRM-F16, PRM-F13, and ADR-PROC-012. |
| C14 | Process runtime should handle eligible pools, backups, and fallback routes instead of only single hard bindings | Captured by PRM-F07, PRM-F16, and runtime model additions. |
| C15 | Governance should prevent self-approval or conflicting duty combinations where needed | Captured by PRM-F06, US-PRM-068, and R-PRM-16. |
| C16 | Template/version snapshotting must preserve audit and later training replay | Captured by ADR-PROC-013, PRM-F16, and runtime/journal updates. |
| C17 | Wave ordering must be coherent; the Wave 1 designer cannot depend on Wave 2 handoff chrome | Captured by ADR-PROC-014 and the corrected PRM-F09 dependency set. |
| C18 | Jasné vlastnictví procesu | Captured by PRM-F17, PRM-F20; see `05-manifest/operational-coverage.json`. |
| C19 | End-to-end pohled místo pohledu oddělení | Captured by PRM-F17, PRM-F19; see `05-manifest/operational-coverage.json`. |
| C20 | Rozdíl mezi procesem a organizační strukturou | Captured by PRM-F03, PRM-F17; see `05-manifest/operational-coverage.json`. |
| C21 | Měření správných metrik | Captured by PRM-F19, PRM-F14; see `05-manifest/operational-coverage.json`. |
| C22 | Variabilita a výjimky | Captured by PRM-F18, PRM-F21; see `05-manifest/operational-coverage.json`. |
| C23 | Kvalita vstupů do procesu | Captured by PRM-F04, PRM-F18; see `05-manifest/operational-coverage.json`. |
| C24 | Procesní disciplína vs. procesní byrokracie | Captured by PRM-F18, PRM-F20; see `05-manifest/operational-coverage.json`. |
| C25 | Role středního managementu | Captured by PRM-F17, PRM-F20; see `05-manifest/operational-coverage.json`. |
| C26 | Reálná práce vs. proces na papíře | Captured by PRM-F21; see `05-manifest/operational-coverage.json`. |
| C27 | Rozhraní mezi procesy | Captured by PRM-F17; see `05-manifest/operational-coverage.json`. |
| C28 | Prioritizace procesů | Captured by PRM-F17, PRM-F20; see `05-manifest/operational-coverage.json`. |
| C29 | Proces bez vazby na strategii | Captured by PRM-F17, PRM-F20; see `05-manifest/operational-coverage.json`. |
| C30 | Záměna standardizace za rigiditu | Captured by PRM-F18; see `05-manifest/operational-coverage.json`. |
| C31 | Nedostatečná procesní gramotnost lidí | Captured by PRM-F20; see `05-manifest/operational-coverage.json`. |
| C32 | Chybějící governance změn procesu | Captured by PRM-F20, PRM-F02; see `05-manifest/operational-coverage.json`. |
| C33 | Nepřiznané neformální mocenské struktury | Captured by PRM-F21; see `05-manifest/operational-coverage.json`. |
| C34 | Absence decision rights | Captured by PRM-F18, PRM-F06; see `05-manifest/operational-coverage.json`. |
| C35 | Neřešení kapacit a úzkých míst | Captured by PRM-F19, PRM-F07; see `05-manifest/operational-coverage.json`. |
| C36 | Podcenění času předávek a čekání | Captured by PRM-F19; see `05-manifest/operational-coverage.json`. |
| C37 | Nepochopení, pro koho je proces zákazník | Captured by PRM-F17, PRM-F19; see `05-manifest/operational-coverage.json`. |
| C38 | Budoucí agenti se mají zavazbovat primárně skrze modelovaný proces | Captured by PRM-F22 and ADR-PROC-021. |
| C39 | Triage může rozhodovat, ale jeho routing musí zůstat součástí procesu | Captured by PRM-F22 and the triage decision model. |
| C40 | Na procesu má být vidět co se děje i za běhu | Captured by PRM-F24 and runtime overlay guidance. |
| C41 | CRM-HR musí zůstat kanonickým zdrojem business rolí a agentů | Captured by ADR-PROC-022, PRM-F16, and PRM-F23. |
| C42 | Workspace musí zůstat kanonickým zdrojem provider profilů | Captured by Workspace integration and ADR-PROC-024. |
| C43 | Budoucí AgentFramework sessions/logs/metrics musí být svázané s procesním kontextem | Captured by ADR-PROC-023 and PRM-F23. |
| C44 | Projects a Processes se nesmí potichu slít do jedné druhé skryté plánovací vrstvy | Captured by ADR-PROC-026, updated repo-fit analysis, and PRM-F10/PRM-F22. |
