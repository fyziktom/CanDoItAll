# Workflow API Skill And Reinstall Setup

## Status

- `Completed`

## Objective

- Add a workflow API Codex skill that matches the project-structure and processes API skill pattern and is discoverable by repo skill sync.

## Covered Inputs

- N002
- N003
- N004
- R003
- R004
- R005

## Prerequisites

- Subbundle 01 route list is implemented or intentionally blocked with a stable route inventory.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\skills\candoitall-api-project-structure\SKILL.md`
- `C:\repositories\CanDoItAll\codex\skills\candoitall-api-processes\SKILL.md`
- `C:\repositories\CanDoItAll\codex\scripts\install-candoitall-skills.ps1`
- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs`

## Deliverables

- New workflow API skill.
- Confirmation that generic repo skill sync includes the new skill.
- OpenAI docs validation note in execution report.

## Dependency Impact

- Subbundle 03 local setup depends on the skill folder existing and being discoverable by the reinstall script.

## Validation Depth

- Skill and setup foundation.

## Implementation Steps

1. Compare existing API skills and mirror their structure.
2. Create `codex/skills/candoitall-api-workflows/SKILL.md`.
3. Validate frontmatter and content against official OpenAI skill docs.
4. Confirm `tools/Reinstall-CanDoItAllMcps.ps1` scans `codex\skills` recursively.

## Scope Exceptions

- Do not add `agents/openai.yaml`; OpenAI docs mark it optional and this skill has no special UI/tool dependency.

## Do Not Do

- Do not duplicate long API examples from tests.
- Do not document routes that are not actually implemented.

## Acceptance Checklist

- Skill name is `candoitall-api-workflows`.
- Description front-loads workflow API trigger words.
- Primary route list matches `WorkflowsApi`.
- Validation section tells agents to use Swagger/OpenAPI and read back specific definitions/runs.

## Proof Required

- `Test-Path codex\skills\candoitall-api-workflows\SKILL.md`
- PowerShell or script proof that repo skill sync discovers the skill.

## Browser Validation Logging

- N/A - skill documentation and script discovery only.

## Progression Gate

- Pass only when the new skill is repo-managed and the reinstall script will sync it without a hard-coded edit.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Add the workflow API skill based on the existing project-structure and processes API skills. Validate the structure against official OpenAI Codex skill docs and confirm the repo MCP reinstall script will sync it.
```
