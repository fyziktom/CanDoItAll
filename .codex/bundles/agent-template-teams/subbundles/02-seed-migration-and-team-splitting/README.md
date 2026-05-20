# Seed Migration And Team Splitting

## Status

- `Completed`

## Objective

- Make the seed system consume `Templates/Agents` for default agents and teams, then remove obsolete hardcoded default-agent assets.

## Success Criteria

- `SandboxWorkspaceSeedBuilder` materializes default agents and teams from the loader.
- Provider keys and capability keys resolve against existing seed catalogs.
- `SandboxWorkspaceSeedNormalizer` refreshes template-backed agents and merges seeded teams.
- Embedded default-agent instruction assets and old managed-template-key lists are removed.

## Covered Inputs

- R004: preserve existing default behavior and metadata.
- R006: load default agents and teams during seed creation.
- R007: remove obsolete hardcoded defaults.
- R008: merge/refresh seeded teams.

## Prerequisites

- SB01 template pack and loader must build.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedNormalizer.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets`
- `C:\repositories\CanDoItAll\Templates\Agents`

## Deliverables

- Template-backed seed materialization.
- Seeded default agent team definitions.
- Normalizer support for seeded team merge/refresh.
- Removed embedded default-agent instruction assets.

## Dependency Impact

- SB03 depends on this migration for test and browser-visible behavior; weak proof here would make UI validation ambiguous.

## Validation Depth

- Critical migration

## Implementation Steps

1. Replace hardcoded default-agent construction with template materialization helpers.
2. Resolve provider/capability references from template keys.
3. Generate `AgentTeam` seed output from team templates.
4. Update normalizer merge behavior for seeded teams.
5. Remove embedded agent instruction asset manifest entries and files.
6. Audit source for obsolete hardcoded default-agent keys/assets.

## Scope Exceptions

- Test helper `new AgentDefinition` usages and runtime-created user agents remain legitimate code paths.

## Do Not Do

- Do not remove support for user-authored or runtime-created agents.
- Do not hardcode new team/member data in C# as a replacement for the old code.
- Do not change unrelated seed catalogs unless required by template resolution.

## Acceptance Checklist

- Seed factory returns the expected default teams and members.
- Existing default agent template keys still resolve to agents.
- Source audit no longer finds old embedded agent instruction asset keys.
- Normalizer tests account for seeded default teams.

## Proof Required

- Source audit command for old asset keys and hardcoded managed template lists.
- Integration test proving seeded default teams.
- `proof/SB02/manifest.md` with changed-file hashes and command transcripts.
- Captured proof manifest: `proof/SB02/manifest.md`.

## Browser Validation Logging

- N/A for direct code migration; app-visible browser proof is owned by SB03 after tests pass.

## Progression Gate

- SB03 may proceed only after targeted seed/team tests pass and source audit confirms obsolete hardcoded defaults are gone.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
