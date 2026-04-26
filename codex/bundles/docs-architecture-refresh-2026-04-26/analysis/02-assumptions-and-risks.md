# Assumptions And Risks

## Assumptions

- Documentation should describe the current repository state on 2026-04-26.
- Project README generation can use a consistent template when it is still accurate and project-specific enough.
- Because this is documentation work, no browser validation is required unless a rendered docs page or app UI is changed. Markdown files are the primary artifact.

## Critical Path Risks

- If the process/agent runtime is summarized too loosely, future agents may misunderstand the durable outbox, recovery, artifact projection, and CRM/HR-to-AgentFramework binding path.
- If generated project READMEs overstate functionality, they become new stale docs immediately. Keep them concise and point to project dependencies and source boundaries.
- Some tracked project directories are not listed in `CanDoItAll.slnx`; README coverage must include them but should avoid implying they are part of the default solution build when they are not.

## Validation Risks

- Mermaid dialects (`architecture-beta` and C4) are renderer-dependent. The validation target is syntactic clarity and use of supported Mermaid block names, not rendered image proof.
- Markdown link validation can be noisy on absolute Windows paths. Prefer relative links in user-facing docs and exact absolute paths inside the bundle contract.
- A full `dotnet build` may surface unrelated code issues in a docs-only change. If build is skipped or fails for unrelated reasons, record the exact result and still run documentation-specific checks.

## Reopen Triggers

- A project directory with a tracked `.csproj` still lacks `README.md`.
- The architecture-beta doc omits `architecture-beta`, C4, or sequence diagrams.
- The process AI-agent execution flow in docs contradicts the actual path through `ProcessesService`, `ProcessRunAutomationDispatchService`, `AgentFrameworkAiTechnicalAgentBridge`, or `AgentFrameworkWorkspaceExecutionService`.
- The root README fails to link to the detailed architecture doc or gives stale module/component shape.
- The final closure table cannot map each raw user requirement to changed files and proof.
