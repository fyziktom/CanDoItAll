# API Skills Refresh And Active Skill Sync

## Status

- `Completed`

## Objective

- Update repo-managed API skills and synchronize active local skill copies so agents use current route, DTO, provider, and tool guidance.

## Success Criteria

- Agents, Workflows, Processes, Project Structure, and Cognitive Memory API skills include current route tables, DTO field maps, examples, and caveats.
- Skills reflect SB03 tool parity decisions instead of implying unsupported direct runtime calls.
- Active local skill copies hash-match repo copies after edits.
- Plugin/project skill coverage decision is recorded.

## Covered Inputs

- RQ-005 API skills refresh and active sync.
- GAP-005 through GAP-009, GAP-012, GAP-013, GAP-016.

## Prerequisites

- SB01 inventory reviewed.
- SB02 API contract proof complete.
- SB03 tool parity decisions complete.
- SB04 docs refresh complete.

## Exact Source References

- `repo://codex/skills/candoitall-api-agents/SKILL.md`
- `repo://codex/skills/candoitall-api-workflows/SKILL.md`
- `repo://codex/skills/candoitall-api-processes/SKILL.md`
- `repo://codex/skills/candoitall-api-project-structure/SKILL.md`
- `repo://codex/skills/candoitall-api-cognitive-memory/SKILL.md`
- `repo://src/CanDoItAll.Web/Api/AgentsApi.cs`
- `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs`
- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`
- `repo://src/CanDoItAll.Web/ProjectStructureAgentApi.cs`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.ContractEndpoints.cs`
- `bundle://inventories/api-docs-skills-gap-map.xlsx`

## Deliverables

- Updated repo skill files.
- Active local skill files synchronized after repo edits.
- Hash proof for each updated skill pair.
- Recorded decision for plugin/project API skill coverage.

## Dependency Impact

- SB06 drift guardrails depend on updated skill expectations.
- Future agent runs depend on active skill sync, not only repo edits.

## Validation Depth

- Enablement and active-runtime consistency.

## Implementation Steps

1. Review the workbook API Inventory, DTO Map, Docs Skills, and Tool Parity sheets.
2. Update each API skill with route groups, DTO fields, examples, and known HTTP-only exceptions.
3. Decide whether plugin/project APIs need dedicated skills or should remain in general control-plane docs.
4. Copy or otherwise synchronize updated skill files to the active skill root.
5. Compute hashes for repo and active copies.
6. Record hash proof and residual exceptions in the execution report.

## Scope Exceptions

- Do not edit runtime source unless a skill update reveals a contract error; reopen SB02 or SB03 instead.
- Do not update bundled workflow/preparation skills unless this initiative explicitly changes them.

## Do Not Do

- Do not leave active local skill copies stale.
- Do not use route prose without exact route tables for large surfaces.
- Do not claim new integrations should use legacy Cognitive Memory paths; prefer v1 and document legacy compatibility.

## Acceptance Checklist

- Each primary API skill has current route and DTO coverage.
- Tool parity or HTTP-only exceptions are reflected in skills.
- Skill hashes match between repo and active root.
- Plugin/project skill decision is recorded.

## Proof Required

- Hash commands for repo and active `SKILL.md` files.
- Workbook regenerated if coverage status changes.
- Execution report mapping skill changes to gap IDs.

## Browser Validation Logging

- `N/A`: skill edits do not change UI.

## Progression Gate

- SB06 may begin only after repo and active skills are synchronized or a concrete blocker is recorded.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Refresh repo-managed API skills from source and workbook findings, sync active local copies, record hash proof, and stop if any skill claim cannot be tied to source, tests, or an explicit exception.
```
