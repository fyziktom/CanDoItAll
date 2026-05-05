# Target Solution

## UI Shape

- Promote `AgentSelectionCard` into the shared presentation for agent cards.
- Update `AgentSwitchDialog` to render `AgentSelectionCard` while preserving search, tag filtering, favorite toggling, current-agent labels, and dialog close-on-select.
- Rework `AgentCatalogPanel` into a card-led Agents tab surface with search/filter summary and a New agent action.
- Use card double-click to open a full DialogService modal for agent editing.

## Dialog Editor

- Add an Agent Details dialog component in the AgentFramework module.
- Load the selected agent editor model, providers, projects, process definitions, and capability catalog inside the dialog.
- Split the editor into BaseLib `Tabs`:
  - Identity
  - Runtime
  - Project Structure Access
  - Workspace Tools
  - Process Access
  - Skills and MCP
  - Tags
- Keep Save, Clear/New, and Delete actions in the dialog body or footer using existing workspace-service methods.
- Return the saved or deleted result to the Agents tab so the card grid can refresh.

## Capability Assignment

- Use `WorkspaceService.ListCapabilitiesAsync`, `AgentEditorModel.SelectedCapabilityIds`, and `WorkspaceService.SaveAgentAsync`.
- Show attached and available capability cards in the Skills and MCP tab.
- Allow assign/remove for cataloged capabilities, emphasizing Skill and MCP server kinds while leaving other catalog kinds visible if already attached.

## Layout

- Use existing BaseLib layout primitives (`Grid`, `Stack`, `Cluster`, `Split`, `Tabs`, `FormSection`) before adding custom CSS.
- Keep Summary and Instructions as full-width text areas in the Identity tab.
- Give Summary a larger default height than a single-line field and Instructions a substantially larger default height.

## Boundaries

- Do not change the persistence model.
- Do not move canonical ownership away from `IAgentFrameworkWorkspaceService`.
- Do not redesign unrelated tabs on `/agents`.
- Do not create a separate card implementation for the modal and tab.
