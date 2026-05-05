# 04 API Skills Author Install

## Status

- `Completed`

## Objective

Create repo-managed and locally installed skills for the project-structure, processes, and agents APIs.

## Covered Inputs

- Original request items 1 and 5.
- R-001 and R-006.

## Prerequisites

- Subbundle 01 route/API parity decisions are available.
- Subbundle 03 reinstall script keeps repo skill sync.

## Exact Source References

- C:\repositories\CanDoItAll\codex\skills
- C:\repositories\CanDoItAll\codex\scripts\install-candoitall-skills.ps1
- C:\Users\lucys\.codex\skills

## Deliverables

- `candoitall-api-project-structure` skill.
- `candoitall-api-processes` skill.
- `candoitall-api-agents` skill.
- Local installed copies under `C:\Users\lucys\.codex\skills`.

## Dependency Impact

- Future Codex runs depend on these skills instead of removed MCP skills.
- Reinstall script skill sync must copy the new skills to other machines.

## Validation Depth

- File inspection and local skill directory inspection.

## Implementation Steps

1. Add three `SKILL.md` files under repo-managed skill root.
2. Remove or replace MCP-specific process skill guidance.
3. Copy/sync skills into local Codex skill root.

## Do Not Do

- Do not instruct agents to reinstall or use the removed MCPs.
- Do not include secrets or local tokens in skill content.

## Acceptance Checklist

- Skills describe base URL, Swagger/OpenAPI discovery, optional JWT bearer auth, and focused endpoint usage.
- Preserved MCP guidance is present in API terms.
- Local installed skill names are visible.

## Proof Required

- Source and local skill listing in execution report.

## Closure Proof

- Added repo skills `candoitall-api-project-structure`, `candoitall-api-processes`, and `candoitall-api-agents`.
- Removed old `candoitall-processes-mcp` repo/local skill content.
- Ran `codex\scripts\install-candoitall-skills.ps1 -SkipPublicSkills`; local skill directories now include the three API skills.

## Browser Validation Logging

- Not UI-relevant.

## Progression Gate

- Closure validation can proceed after repo and installed skills match.

## Suggested Agent Prompt

Author concise, operational API skills that replace the removed MCP skills and preserve the important MCP tool instructions.
