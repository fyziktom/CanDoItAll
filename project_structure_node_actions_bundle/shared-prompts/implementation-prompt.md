# Implementation Prompt

Implement the current subbundle only.

Before editing, read:

- `C:/repositories/CanDoItAll/project_structure_node_actions_bundle/README.md`
- `C:/repositories/CanDoItAll/project_structure_node_actions_bundle/plan/01-phase-plan.md`
- the selected subbundle README
- `C:/repositories/CanDoItAll/project_structure_node_actions_bundle/traceability/01-requirement-traceability.md`

Rules:

- Preserve the literal runtime requirement: runtime-capable nodes must offer both normal run and administrator run whenever a launch plan resolves.
- Use existing Workbench services for execution: `IProjectStructureRuntimeLauncher`, `IProjectStructureLocalFileOpener`, and `OpenArtifactInNewTabAsync`.
- Do not add MCP/internal-agent tools that launch local PowerShell, UAC, File Explorer, or browser tabs.
- Keep UI changes consistent with CanvasLib `CanvasWorkbenchAction` and the existing quick-action dialog.
- Update `reviews/01-execution-report.md` while proof is fresh.

Validation:

- Run the targeted tests named in the subbundle.
- Capture browser proof for UI subbundles.
- Record host-proof limitations honestly.
