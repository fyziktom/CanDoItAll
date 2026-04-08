# Host integration reinstall and skill guidance

## Status

- `Ready`

## Objective

- Ship the new tool surface end to end by updating reinstall wiring, generated MCP config, and Codex-facing skill guidance.

## Covered Inputs

- `REQ-06`
- `REQ-07`
- User expectation that Codex should know how to use the updated MCP properly

## Prerequisites

- `subbundles/02-project-and-solution-navigation-parity`
- `subbundles/03-member-behavior-and-source-inspection-parity`

## Exact Source References

- C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1
- C:\repositories\CanDoItAll\codex\README.md
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\Program.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.CodeAnalytics\CodeAnalyticsTools.cs

## Deliverables

- Updated reinstall flow and generated configuration for the expanded CodeAnalytics MCP.
- Repo-managed skill or documentation updates that explain how to choose the new tools.
- Explicit note on whether a restart is needed before final proof.

## Dependency Impact

- The final rerun cannot start until the updated MCP is installed.
- Weak skill guidance will cause future sessions to underuse the new tool surface and recreate the same benchmark gaps.

## Validation Depth

- `Process-critical rollout`

## Implementation Steps

1. Update host integration files to include any newly added MCP tools or supporting models.
2. Update the reinstall script and generated config paths if the tool surface changed.
3. Add or update repo-managed skill guidance for CodeAnalytics tool selection.
4. Reinstall the MCP and record whether a Codex restart is now required.

## Scope Exceptions

- If a restart is required, the rerun waits for the user to restart Codex before SB-05 can close.

## Do Not Do

- Do not defer reinstall updates to a later step.
- Do not add vague skill notes that fail to explain when to use the new tools.

## Acceptance Checklist

- Reinstall succeeds with the expanded CodeAnalytics MCP.
- Generated config still registers `candoitall_codeanalytics`.
- Repo guidance explains how to use the new parity tools.

## Proof Required

- `powershell -ExecutionPolicy Bypass -File .\tools\Reinstall-CanDoItAllMcps.ps1`
- Captured proof that the installed CodeAnalytics entrypoint was refreshed
- Recorded restart requirement decision

## Browser Validation Logging

- N/A

## Progression Gate

- The updated MCP is published and registered, and the restart requirement is explicitly known.

## Suggested Agent Prompt

```text
Implement the host integration and skill guidance subbundle only. Reinstall must be part of the same pass, and if a restart is required, say so explicitly before pretending final proof is possible.
```
