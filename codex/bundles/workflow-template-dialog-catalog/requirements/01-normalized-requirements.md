# Normalized Requirements

| Requirement | Normalized Requirement | Acceptance Proof |
| --- | --- | --- |
| `REQ-001` | Remove the primary Templates tab from the Workflows page flow. | Component test proves no `workflows-tab-templates`; browser screenshot shows no Templates tab. |
| `REQ-002` | Add a template catalogue button on the Workflows tab. | Component test and browser screenshot show button in the Workflows tab/catalog area. |
| `REQ-003` | Template pack loads only when the catalogue dialog opens. | Component test with failing/observable loader proves page init and unrelated tab changes do not call `Load()`; opening the dialog does. |
| `REQ-004` | Catalogue dialog shows template name, basic description, count/seed metadata, and Preview action. | Component test validates dialog content; Playwright screenshot validates open-state layout. |
| `REQ-005` | Preview action opens a separate dialog with a workflow canvas visualization. | Component test validates preview dialog and canvas presence; Playwright screenshot validates canvas readability. |
| `REQ-006` | Preview dialog lets users add the selected template to drafts. | Component test proves draft is saved with `WorkflowLifecycleStatus.Draft`; UI exposes "Add to my drafts". |
| `REQ-007` | Repeated draft adoption of the same workflow uses `01`, `02`, etc. prefixes when the base name already exists. | Component or unit test covers base, first collision, and second collision. |
| `REQ-008` | New dialogs use existing CanDoItAll/BaseLib components and stay close to generated proposals. | Source review plus screenshot comparison notes cite shared component usage and visual match/gaps. |
| `REQ-009` | Rename/debrand SEAMARK workflow templates into generic offer-analysis examples. | Template tests and source search prove no `SEAMARK` remains in workflow templates or UI-facing smoke labels. |
| `REQ-010` | Keep validation large-screen only. | Execution report browser analytics record large-screen viewport and state that smaller passes were skipped by user constraint. |
