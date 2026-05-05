# Implementation Prompt

Implement the selected subbundle only.

- Preserve the raw request language in `inputs/00-original-request.md`.
- Use existing BaseLib components and current AgentFramework service contracts.
- Keep the switch-agent modal and Agents tab using one shared `AgentSelectionCard`.
- Keep technical-agent persistence through `IAgentFrameworkWorkspaceService`.
- Update focused component tests alongside behavior changes.
- Record proof and gate decisions in `reviews/01-execution-report.md` before closing the subbundle.
