# Input Coverage Matrix

| Raw note id | Exact wording summary | Normalized requirements | Owning subbundle | Planned proof | Exception status |
| --- | --- | --- | --- | --- | --- |
| `N001` | Codex gets tasks directly from the project-structure mindmap. | `R001`, `R004`, `R009`, `R010` | `01`, `03` | Filtered read tools and checklist integration tests | `None` |
| `N002` | Codex creates subprojects and uses them for larger work organization. | `R004`, `R014` | `01`, `03` | Project and subproject mutation tests plus MCP chain | `None` |
| `N003` | Codex writes status, troubles, plans, and approvals back into project structure. | `R004`, `R006`, `R014` | `01`, `03` | Node update and approval-request proof | `None` |
| `N004` | Codex improves existing plans and uses project structure for planning and reporting. | `R009`, `R010`, `R011`, `R014` | `01`, `02`, `03` | Checklist and knowledge tool proof | `None` |
| `N005` | Import from XMind, Mermaid, DOCX, and similar tools should be possible. | `R015` | `01`, `04` | Import seam plus first-pass format proof | `Potential format-specific warnings must be explicit if not all examples ship in v1.` |
| `N006` | Asset reads are readonly and changes create new asset nodes under the original asset node. | `R013` | `01`, `04` | Asset revision chain proof | `None` |
| `N007` | Prevent another agent from working in the same repository and branch. | `R007`, `R008` | `01`, `03` | Lease conflict tests using repo-branch scope | `None` |
| `N008` | Filter nodes and omit nonessential graph data unless requested. | `R009` | `03` | Tool-shaping tests | `None` |
| `N009` | Checklist query must include unfinished items, prerequisites, and priority propagation. | `R010` | `01`, `04` | Checklist service tests and end-to-end readback | `None` |
| `N010` | Provide project-management knowledge and mission guidance now, with future knowledge-db extensibility. | `R011`, `R012` | `02` | Knowledge API and MCP read proof | `None` |
| `N011` | Add settings in CanDoItAll web for agent permissions and estimate-based approvals. | `R005`, `R006`, `R017` | `02` | Browser and policy tests | `None` |
| `N012` | Multiple machines run local MCP instances that connect to the main CanDoItAll machine. | `R002`, `R017` | `03`, `04` | MCP settings template, setup script, and integration proof | `None` |
| `N013` | Validation must chain real creation and readback of delivery block and document assets and capture analytics. | `R018`, `R019`, `R020` | `04` | End-to-end validation report | `None` |
