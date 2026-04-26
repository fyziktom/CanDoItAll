# QA Prompt

Review the documentation refresh as a source-grounding and coverage task.

Validate:

- Every raw note is mapped to changed files and proof.
- `docs/architecture-beta.md` contains `architecture-beta`, C4, and sequence diagrams.
- The process AI-agent narrative matches the real flow through `ProcessesService`, `ProcessRunAutomationDispatchService`, `AgentFrameworkAiTechnicalAgentBridge`, `AgentFrameworkWorkspaceExecutionService`, and `MafAgentRuntime`.
- Root README has a current overview diagram and links to detailed docs.
- Shared-component docs describe the current split library architecture.
- Every tracked `.csproj` directory under `src`, `tests`, and `tools` has `README.md`.
- `git diff --check` passes.
