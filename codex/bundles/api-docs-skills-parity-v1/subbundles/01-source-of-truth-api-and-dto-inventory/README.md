# Source Of Truth API And DTO Inventory

## Status

- `Completed`

## Objective

- Establish the durable source inventory that all later API, docs, skills, and tool repairs must use.

## Success Criteria

- The XLSX workbook contains current route counts, endpoint inventory, gap map, DTO map, docs/skills status, tool parity, phase plan, and validation commands.
- Route counts are regenerated from source and recorded in the execution report.
- Downstream subbundles can trust the source counts or know exactly when to reopen this phase.

## Covered Inputs

- Raw request for detailed missing/obsolete/API/DTO/docs/skills analysis.
- Raw request to use XLSX to map gaps.
- RQ-001 source-of-truth inventory.

## Prerequisites

- none

## Exact Source References

- `repo://src/CanDoItAll.Web/Api/AgentsApi.cs`
- `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs`
- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`
- `repo://src/CanDoItAll.Web/ProjectStructureAgentApi.cs`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.cs`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.ContractEndpoints.cs`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApiDtos.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProcessTools.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProjectStructureTools.cs`
- `bundle://inventories/api-docs-skills-gap-map.xlsx`

## Deliverables

- Regenerated `bundle://inventories/api-docs-skills-gap-map.xlsx`.
- Rendered workbook summary proof at `bundle://inventories/api-docs-skills-gap-map-summary.png`.
- Preserved workbook builder at `bundle://inventories/build-gap-map.mjs`.
- Updated inventory notes when route, DTO, tool, docs, or skill findings change.

## Dependency Impact

- SB02, SB03, SB04, SB05, SB06, and SB07 depend on this inventory.
- If source route counts change, downstream docs, skills, tests, and drift guardrails must be rechecked before continuing.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Run `.codex/tmp/api-docs-skills-gap-map/build-gap-map.mjs` with the bundled Node runtime.
2. Confirm route counts in the Summary and Surface Counts sheets.
3. Review the Gap Map, DTO Map, Docs Skills, and Tool Parity sheets for stale findings.
4. Update bundle analysis and traceability if source changes alter priorities or subbundle ownership.
5. Record command output and artifact paths in `reviews/01-execution-report.md`.

## Scope Exceptions

- Do not implement API, docs, skills, or runtime tool repairs in this phase.

## Do Not Do

- Do not hand-edit workbook cells when the generator can produce them.
- Do not treat exact route text coverage as semantic proof of complete documentation.
- Do not continue downstream if route counts disagree with source.

## Acceptance Checklist

- XLSX exists and opens.
- Summary PNG is rendered.
- Focused control-plane route count excluding `/api/access` and Cognitive Memory v1 aliases is recorded.
- Critical gaps are mapped to owning subbundles.
- Execution report records the regeneration command.

## Proof Required

- `node .codex\tmp\api-docs-skills-gap-map\build-gap-map.mjs`
- `bundle://inventories/build-gap-map.mjs`
- `bundle://inventories/api-docs-skills-gap-map.xlsx`
- `bundle://inventories/api-docs-skills-gap-map-summary.png`
- `bundle://inventories/api-docs-skills-gap-map-inspect.json`

## Browser Validation Logging

- `N/A`: this subbundle produces workbook artifacts and does not change UI.

## Progression Gate

- Downstream subbundles may start only after the workbook has been regenerated and reviewed against current source.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Regenerate the workbook from source, verify the route counts and sheets, update the execution report with proof, and stop if the generated inventory disagrees with current C# route registrations.
```
