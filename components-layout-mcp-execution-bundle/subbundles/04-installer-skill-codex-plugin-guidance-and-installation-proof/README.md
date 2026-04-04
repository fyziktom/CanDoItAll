# Installer, skill, codex plugin guidance, and installation proof

## Status

- `Completed`

## Objective

- Wire the components MCP into the standard install flow, add repo-managed skill and plugin guidance for using it during CanDoItAll component work, and prove installation locally.

## Covered Inputs

- User request to ensure the components MCP installs with the MCP install script.
- User request to test that installation here.
- User request to add instructions, a skill, and a Codex plugin for using the component MCP properly.
- Team goal to reduce ad-hoc structural CSS by steering work through shared components first.

## Prerequisites

- Subbundle `03-candoitall-mcp-components-layout-knowledge-and-component-guidance` must expose the guidance the install flow and skill will point to.

## Exact Source References

- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`
- `C:\repositories\CanDoItAll\CanDoItAll.Mcp.Components.settings.json`
- `C:\repositories\CanDoItAll\codex\README.md`
- `C:\repositories\CanDoItAll\codex\scripts\install-candoitall-skills.ps1`
- `C:\repositories\CanDoItAll\codex\skills`
- `C:\repositories\CanDoItAll`

## Deliverables

- MCP reinstall script publishes and configures `CanDoItAll.Mcp.Components`.
- Codex config and VS Code MCP config include the components server.
- New repo-managed skill that teaches Codex how to use the components MCP for shared-component work.
- New Codex plugin surface that packages the guidance for local repo use.
- Local installation proof from the updated install script.

## Dependency Impact

- If this phase is weak, the improved MCP becomes discoverable only to manual users and the repo cannot rely on it as part of the normal onboarding path.

## Validation Depth

- `Process-critical closure`

## Implementation Steps

1. Extend the reinstall script to publish and register the components MCP.
2. Update manifest and config writers to include the new server.
3. Add the new repo-managed skill and any supporting plugin files.
4. Update repo docs so the new guidance is discoverable.
5. Run the install flow locally and verify the components MCP entrypoints and config changes.

## Scope Exceptions

- Do not alter unrelated MCP install behavior beyond what is necessary to support the components server.
- Do not create multiple overlapping skills for the same guidance.

## Do Not Do

- Do not ship the components MCP as a manual-only setup.
- Do not leave the new skill undocumented or uninstalled by the repo scripts.
- Do not create plugin metadata that points at nonexistent assets.

## Acceptance Checklist

- `Reinstall-CanDoItAllMcps.ps1` publishes and registers `CanDoItAll.Mcp.Components`.
- The install manifest records the components MCP install root and entrypoint.
- The repo contains a concise skill for shared-component MCP usage.
- The repo contains a plugin surface for the same guidance.
- Local install proof confirms the components MCP can be installed from the updated script.

## Proof Required

- Run the updated reinstall script locally and capture success output.
- Show resulting config or manifest entries for `candoitall_components`.
- Validate the new repo skill installs through the repo skill install flow.
- Validate any created plugin manifest files exist and are internally consistent.

## Browser Validation Logging

- `N/A`

## Progression Gate

- The install script, config surfaces, skill, plugin, and local proof must all be complete before the bundle can enter final closure validation.

## Suggested Agent Prompt

```text
Implement this subbundle only. Add CanDoItAll.Mcp.Components to the standard reinstall flow, create the repo-managed skill and plugin guidance for component-first layout work, and prove the install locally.
```
