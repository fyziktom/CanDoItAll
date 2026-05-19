# Normalized Requirements

| Id | Requirement | Acceptance Signal | Owning Subbundle |
| --- | --- | --- | --- |
| `R001` | AgentFramework supports durable team records with name, optional description, and agent memberships. | Teams save/load through the organization workspace catalog. | `01` |
| `R002` | A team can contain multiple agents. | Team detail/membership view lists multiple assigned agents. | `01`, `02` |
| `R003` | An agent can belong to multiple teams. | Same agent appears under at least two team nodes and in both filtered views. | `01`, `02` |
| `R004` | Team creation and management live in the Agents module on the `/agents` Agents tab. | Agents tab exposes create/edit/delete/manage-team controls. | `02` |
| `R005` | Agents tab contains a tree view showing all agents and teams with agents under each team. | `TreeView` renders all-agents root plus team nodes and agent child nodes. | `02` |
| `R006` | Clicking a team node filters the visible agent cards to that team. | Selecting a team node changes the card grid count and selected filter state. | `02` |
| `R007` | Team membership is edited through a modal with multi-selection by clicking agent cards. | Modal allows toggling multiple `AgentSelectionCard` cards and confirming membership. | `02` |
| `R008` | The team membership modal follows the switch-agent card pattern. | Modal uses `AgentSelectionCard` with compact grid, search, and visible selected state. | `02` |
| `R009` | Process launch HR matching can be run with a selected delivery team. | Launch planning detail opens an HR matching dialog or control with team selection. | `03` |
| `R010` | HR matching prefers selected-team agents for required process roles. | In-team candidates receive a team-fit marker and score preference. | `03` |
| `R011` | HR matching still fills required roles with out-of-team agents when the selected team lacks a suitable candidate. | Required roles can select out-of-team candidates instead of remaining gaps. | `03` |
| `R012` | Out-of-team matched candidates are marked in the matching/role candidate UI. | Candidate row/card shows an "Outside selected team" style badge after reload. | `03` |
| `R013` | Existing no-team launch planning behavior is preserved. | Existing launch planning integration tests still pass or targeted no-team regression passes. | `03`, `04` |
| `R014` | UI work has real browser proof. | Execution report includes screenshots/actions for agents tree, membership modal, and launch HR matching. | `04` |
