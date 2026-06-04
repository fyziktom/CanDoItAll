# SB08 - Documentation and operator handoff

## Status

- Status: Completed

## Objective

Update architecture/runtime documentation so future contributors know that process tools are provided by registered runtime tool providers, not MAF hard-coded process builders.

## Covered Inputs

- User request to decouple MAF from Processes in small safe steps.
- `inputs/01-source-artifacts.md`
- `analysis/01-current-state.md`
- `inventories/01-process-tool-parity-inventory.md`
- `evidence/checklists/MAF_Processes_Decoupling_Checklists.xlsx`

## Prerequisites

- SB07 closure gate passed.

## Exact Source References

- `repo://README.md`
- `repo://docs/architecture-beta.md`
- `repo://src/CanDoItAll.AgentFramework.Maf/README.md`
- `repo://src/CanDoItAll.Modules.Processes/README.md`
- `repo://docs/api-control-plane.md`
- `repo://codex/skills`

## Deliverables

- Updated docs describing new seam.
- Removed stale references to `MafAgentRuntime.ProcessToolBuilder`.
- Added next-phase note for process core split / driver pack extraction.
- Operator handoff note explaining how to debug missing process tools.

## Dependency Impact

- Docs prevent future regressions and reduce chance Codex reintroduces old coupling.


## Validation Depth

- Standard validation. Requires source assertions, build/test proof, and execution-report updates.


## Implementation Steps

1. Search docs for `ProcessToolBuilder`, `MafAgentRuntime.ProcessTools`, and `CanDoItAll.Modules.Processes` in MAF context.
2. Update MAF README process automation note.
3. Update Processes README to mention `ProcessAgentRuntimeToolProvider`.
4. Add architecture note showing target dependency direction.
5. Do not claim process core split or drivers are complete.
6. Add troubleshooting section: provider not registered, process tools absent, approval wrapping changed.

## Scope Exceptions

- Full process-core split is intentionally out of scope.
- Full driver-pack architecture is intentionally out of scope.

## Do Not Do

- Do not change process dispatcher behavior.
- Do not start process core extraction.
- Do not introduce DotNet/SWDev/business process drivers.
- Do not remove or rename any process tool.

## Acceptance Checklist

- [x] No stale `MafAgentRuntime.ProcessToolBuilder` documentation remains.
- [x] Docs describe provider seam accurately.
- [x] Docs identify next phase without claiming it is done.
- [x] Operator troubleshooting is actionable.

## Closure Notes

- Entry gate: Passed. SB07 completed real app-composition runtime smoke before docs were updated.
- Validation: Live stale-reference scan, broader bundle-history scan, documentation source assertions, `git diff --check`, and solution build passed.
- Browser validation: N/A. SB08 is documentation-only and did not render or modify a UI route.
- Proof: `bundle://proof/SB08/manifest.md`.
- Progression gate: Passed. SB09 may perform final red-team and next-phase readiness.

## Proof Required

- `rg "ProcessToolBuilder|MafAgentRuntime.ProcessTools" README.md docs src codex` transcript
- Documentation source assertions
- `proof/SB08/manifest.md`

## Browser Validation Logging

- N/A. Documentation-only change; no rendered UI route was exercised.


## Progression Gate

- Passed. Docs match the implemented provider seam and explicitly avoid claiming the process-core or driver-pack split is complete.


## Suggested Agent Prompt

Use `shared-prompts/implementation-prompt.md`. Focus only on SB08. Do not start the next subbundle until the SB08 closure gate passes and proof artifacts are written.
