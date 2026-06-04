# Documentation Refresh

## Status

- `Completed`

## Objective

- Update living docs so they match current API routes, DTOs, provider capabilities, tool parity decisions, and historical proof status.

## Success Criteria

- API control-plane docs include all relevant API skills and surface coverage decisions.
- Cognitive Memory docs state the current 38-route legacy and v1 surfaces and include missing operation routes.
- Process operator runbook covers current DTOs and runtime diagnostics enough for operators.
- Provider capability and model-parameter behavior are documented where it affects agent/API use.
- Historical proof docs are clearly marked as historical or superseded.

## Covered Inputs

- RQ-004 documentation refresh.
- GAP-002, GAP-004, GAP-012, GAP-014.
- Docs side of the raw request.

## Prerequisites

- SB01 inventory reviewed.
- SB02 API contract decisions complete.
- SB03 tool parity decisions complete.

## Exact Source References

- `repo://docs/api-control-plane.md`
- `repo://docs/cognitive-memory/operations/api.md`
- `repo://docs/process-agent-operator-runbook.md`
- `repo://docs/agent-runtime-hardening-verification.md`
- `repo://src/CanDoItAll.Web/Api/CognitiveMemoryApi.ContractEndpoints.cs`
- `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs`
- `repo://src/CanDoItAll.AgentFramework.Models`

## Deliverables

- Updated docs with route counts, route groups, DTO field references, provider capability notes, and skill links.
- Historical framing for dated proof docs.
- Execution report showing source files used and validation commands.

## Dependency Impact

- SB05 skills should align with refreshed docs and avoid duplicating stale claims.
- SB06 drift guardrails should use the updated docs as current expected coverage.

## Validation Depth

- Documentation and source-proof validation.

## Implementation Steps

1. Review workbook Gap Map and Docs Skills sheets.
2. Update `docs/api-control-plane.md` with all relevant API skills and surface decisions.
3. Update Cognitive Memory operations API docs from current contract/source.
4. Update process operator runbook for current DTO and runtime details.
5. Add provider capability and model-parameter notes where the docs currently omit current source behavior.
6. Add historical/superseded framing to dated proof docs.
7. Run markdown/source sanity checks and record proof.

## Scope Exceptions

- Do not edit repo skills in this phase except to note needed follow-up for SB05.
- Do not change API behavior in docs-only work.

## Do Not Do

- Do not manually invent route counts; use source/workbook counts.
- Do not leave proof records looking like current operational docs.
- Do not duplicate full source DTOs when compact field tables are enough.

## Acceptance Checklist

- Docs route counts match workbook/source.
- Docs link or name the correct API skills for each major surface.
- Provider capability notes include private provider pricing/tags and feature matrix categories.
- Historical proof doc is visibly historical.
- `git diff --check` passes.

## Proof Required

- `git diff --check`
- Source references in changed docs to route/DTO/provider files.
- Execution report entries mapping each changed doc to the gap IDs it closes.

## Browser Validation Logging

- `N/A` unless docs work changes rendered app UI. If UI changes occur, add Playwright evidence.

## Progression Gate

- SB05 may begin only after docs no longer contain known stale route counts or missing high-level skill references.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Refresh docs from source and workbook findings. Keep changes focused, avoid broad rewrites, run markdown checks, and record proof for each closed doc gap.
```
