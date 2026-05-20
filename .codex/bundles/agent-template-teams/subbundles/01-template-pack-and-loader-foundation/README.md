# Template Pack And Loader Foundation

## Status

- `Completed`

## Objective

- Create the editable `Templates/Agents` pack and the loader that makes it the default agent seed source.

## Success Criteria

- `Templates/Agents/manifest.json` exists and references all default team folders.
- Every default agent member folder contains `instructions.md`, `settings.json`, and `skills.json`.
- Loader parses manifest, teams, members, settings, skills, and instructions with structured JSON APIs.
- Instruction files include the preserved role text plus improved editable-template guidance.

## Covered Inputs

- R001: default agent templates under `Templates/Agents`.
- R002: team folders with team metadata/settings.
- R003: per-agent folders/files.
- R005: revised editable instructions.

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\Templates`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds`

## Deliverables

- `Templates/Agents` manifest, team folders, and member files.
- `AgentTemplatePackLoader.cs` with typed template records and repository-root resolution.
- `Templates/README.md` updated to document agent templates.

## Dependency Impact

- SB02 depends on this loader and file shape to materialize seed agents without embedded C# defaults.
- SB03 depends on this pack being complete enough for tests and browser-visible seeded agents.

## Validation Depth

- Critical foundation

## Implementation Steps

1. Export current default agent metadata into `Templates/Agents`.
2. Add team-level `team.json` and per-member `settings.json`, `skills.json`, and `instructions.md`.
3. Append clear template revision notes to each instruction file.
4. Add the loader and ensure it resolves the repo template path reliably.
5. Build affected project and add tests that load the pack.

## Scope Exceptions

- Existing workflow/process templates are not redesigned.
- Runtime user-created agents are not converted into templates.

## Do Not Do

- Do not move provider catalog definitions into templates.
- Do not invent new default agents beyond the migrated baseline.
- Do not hide generic instructions inside code.

## Acceptance Checklist

- All expected files exist under `Templates/Agents`.
- Loader returns every team and member.
- Every member has non-empty instructions, provider key, and capability keys.
- Instructions include revised template guidance.

## Proof Required

- `rg --files Templates\Agents` inventory.
- `dotnet build src\CanDoItAll.AgentFramework.Persistence\CanDoItAll.AgentFramework.Persistence.csproj`.
- Integration test coverage that loads the template pack.
- `proof/SB01/manifest.md` with changed-file hashes and command transcripts.
- Captured proof manifest: `proof/SB01/manifest.md`.

## Browser Validation Logging

- N/A for this subbundle because it is a file/loader foundation; browser proof is owned by SB03.

## Progression Gate

- SB02 may proceed only after the loader can parse the full template pack and build succeeds.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
