# Implementation Prompt

Implement the filesystem-agent-tools bundle phase by phase.

Rules:

- Do not add a `MafAgentRuntime` partial.
- Do not add filesystem behavior directly to `WorkspaceRuntimePlugin`.
- Keep all physical filesystem operations routed through `IWorkspaceFileService`.
- Add direct tests that instantiate the extracted filesystem plugin without constructing `MafAgentRuntime`.
- Keep mutation tools approval protected.
- Update the bundle execution report after each phase.
