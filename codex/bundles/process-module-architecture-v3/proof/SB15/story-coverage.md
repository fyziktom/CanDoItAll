# SB15 Story Coverage

| Story ID | Implemented source | Test proof | Browser proof | Delta from current UX | Remaining risk |
| --- | --- | --- | --- | --- | --- |
| US-005 | `ProcessDefinitionEditorProjectionService` handles `SaveDraft`, `Publish`, `Archive`, and `Delete` typed commands with receipts and lint. | `test-unit-definition-editor-sb15.txt`; `test-components-process-shell-sb15.txt` | `browser/desktop-editor-assertions.json`; `browser/processes-definition-editor-desktop-mcp.png` | Commands are projection/session-backed rather than old service mutation; archive/delete reject template defaults until a draft exists and reject stale version tokens after authored state exists. | Durable authored-definition persistence is intentionally deferred. |
| US-006 | Identity draft projection carries name, scope label, customer, owner, summary, and value statement. | `Editor_projection_reads_authoring_sections_from_template_metadata`; component render and save-command tests. | Browser assertion edits name and owner. | Scope is displayed from projection and not edited directly in SB15. | Project-specific identity persistence remains downstream. |
| US-007 | Governance projection carries criticality, autonomy, operating mode, working status, manager override summary, notes, change summary, and policy summary. | Unit metadata assertion and component command-boundary assertion for manager override. | Browser assertion edits `managerOverride` and screenshot shows the field. | Manager override is an authoring summary in SB15; it is not yet bound to candidate readiness or manager runtime assignment. | SB21/SB24 must bind this field to launch/readiness/operator semantics. |
| US-008 | Contract and simulation projections carry interface contract, constitution rule, operating mode summary, simulation readiness, step count, required role count, and required artifact count. | Unit metadata projection test; publish lint test covers missing contract/simulation blocking behavior. | Browser screenshots show contracts, simulation counts, simulation readiness, and clear lint. | Simulation readiness is projection validation, not a runtime rehearsal engine in SB15. | Runtime rehearsal remains downstream. |

## Acceptance Criteria

| Criterion | Result | Proof |
| --- | --- | --- |
| AC-003 separate planes | Passed | Editor projection, template loader, and UI are separate; UI dependency scans are clean. |
| AC-012 explicit manager/governance inputs | Passed for SB15 scope | Governance fields include policy and manager override summary as explicit projection data. Runtime manager execution is not implemented here. |
| AC-021 UI reads projections | Passed | `ProcessWorkspaceShell.razor` receives editor data through `IProcessWorkspaceProjectionClient`; scans show no UI runtime/persistence access. |
| AC-035 UI/UX preservation | Passed | Existing form concepts are represented as Identity, Governance, Contracts, and Simulation sections using the module's component library. |
| AC-039 user-story map coverage | Passed | US-005 through US-008 are covered in this table. |
| AC-040 browser proof owned by UI subbundle | Passed | Browser MCP and Playwright screenshots/assertions are under `proof/SB15/browser/`. |

## Not Implemented By Design

- Role editor, step editor, canvas composition, launch planning, runtime rehearsal, and operator control remain downstream subbundles.
- Authored definition persistence is not durable yet; SB15 stores scoped authoring session snapshots in the application projection service so command semantics can be proven without introducing an incorrect storage boundary.
- Manager override is captured as typed governance authoring data only. It is not resolved to HR/agent candidates or runtime manager assignments in this subbundle.
