# Implementation Prompt

Implement the bundle phase by phase.

- Preserve existing project/process canvas behavior and window state handling.
- Add shared AgentFramework component code first, then integrate project and process hosts.
- Filter agents through `AgentProjectStructureAccessMetadata` and `AgentProcessAccessMetadata`.
- Reuse `ChatWorkspacePanel`; do not fork its UI.
- Keep code strongly typed and avoid magic-string access checks.
- Add only focused tests that protect filtering and interaction behavior.
- Record proof and screenshots in `reviews/01-execution-report.md`.
