# Target Solution

## API Shape

- Add explicit workflow definition lifecycle endpoints rather than making clients mutate lifecycle through full save payloads:
  - `POST /api/workflows/definitions/{workflowId}/publish`
  - `POST /api/workflows/definitions/{workflowId}/suspend`
  - `POST /api/workflows/definitions/{workflowId}/archive`
- Add portable workflow definition commands:
  - `GET /api/workflows/definitions/{workflowId}/export`
  - `POST /api/workflows/definitions/import`
- Implement lifecycle by loading the current definition and saving a new version with the same graph/runtime policy and the requested lifecycle status. Validate publish before activation; suspend/archive should preserve definition payload.
- Implement import/export with a dedicated envelope DTO that includes schema version, definition, validation result, and exported timestamp. Import should reuse `SaveDefinitionAsync` so validation/version semantics stay centralized.

## Skill Shape

- Add `codex/skills/candoitall-api-workflows/SKILL.md`.
- Follow the existing project-structure and processes API skills: frontmatter, short trigger description, access section, route inventory, operating rules, and validation section.
- Do not add scripts or `agents/openai.yaml` unless a real dependency appears. OpenAI docs identify these as optional.

## Install Shape

- Keep `tools/Reinstall-CanDoItAllMcps.ps1` generic skill sync behavior. If the new skill lives under `codex\skills`, no hard-coded script change is needed.
- Run the reinstall script or skill-sync path to update `%USERPROFILE%\.codex\skills`.
