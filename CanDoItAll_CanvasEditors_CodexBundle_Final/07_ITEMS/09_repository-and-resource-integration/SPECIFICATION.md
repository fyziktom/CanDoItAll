
# Specification

## Item identity

- **Item ID:** I09
- **Title:** Repository nodes and resource integration
- **Origin:** docx
- **Dependencies:** I01, I02

## Objective

Connect repository nodes to the existing resource model so local and remote repositories become reusable assets instead of duplicated data islands.

## Normalized scope

Add repository nodes that can represent remote GitHub repositories and local repositories or folders, with selectors and folder-picking fallbacks.

### In scope

- Repository node creation and editing.
- Remote GitHub connection and repository selection.
- Local repository or folder selection and path fallback.
- Cross-linking to reusable resource records when sensible.

### Out of scope

- A full Git provider synchronization engine.

## Key implementation decisions

- Reuse CanDoItAll.Modules.Resources wherever repository-like references already exist.
- Remote GitHub repositories and local repositories should share one repository node family with mode-specific metadata.
- Folder selection should support browser capabilities but also provide a manual path fallback for unsupported environments.

## Implementation tasks

- Add repository node modes and metadata.
- Reuse or link resource entries when the same repository is already known in Resources.
- Implement UI for remote GitHub selection and local folder/path entry.
- Keep repository display concise on the canvas card while exposing full details in the inspector.

## Risks to control

- Duplicate repository registries drift apart quickly if Resources is not reused.

## Covered original notes

- N069 — Repository
- N070 — Remote
- N071 — GitHub connection
- N072 — Selection of specific repositoriy
- N073 — Local
- N074 — OpenFolder dialog
