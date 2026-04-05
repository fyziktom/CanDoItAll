# 06-service-decomposition-guardrail-tests-and-plugin-gate-review

## Status

- `Prepared for Codex execution`

## Objective

Reduce hotspot risk, add architecture guardrails, and rerun the review in a real .NET environment before reopening the plugin wave.

## Covered Inputs

- `PW6-011`
- `PW6-012`

## Prerequisites

- SB01 through SB05 complete or trusted.
- Plan the minimum public contract stability you want to preserve while shrinking hotspots.

## Exact Source References

- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`
- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/architecture/adrs/ADR-0004-workbench-node-extension-guardrails.md`
- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`

## Evidence Focus

- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs (3227 lines)`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs (5001 lines)`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:1262-1425`
- `architecture/adrs/ADR-0004-workbench-node-extension-guardrails.md`

## Deliverables

- Smaller focused services on the Workbench and CRM/HR seams.
- Architecture guardrail tests covering the key canonical invariants.
- Final rerun review with real build/test/browser evidence.

## Dependency Impact

- Creates the final confidence gate before reopening plugin wave delivery.
- Reduces regression risk when the next big integration modules arrive.

## Validation Depth

- Static architecture review.
- Real dotnet build/test in the target environment.
- Targeted browser/Playwright proof where UI flows changed.

## Implementation Steps

- Split Workbench and CRM/HR hotspots along the new architectural seams.
- Add guardrail tests for no parallel truth, no hierarchy duplication, no metadata foreign-id leakage, valid role-to-node assignments, and plugin registry rules.
- Rerun the canonical-model review and plugin-wave readiness gate.

## Do Not Do

- Do not declare the plugin wave open just because code compiles.
- Do not rely only on manual review where guardrail tests can exist.

## Acceptance Checklist

- [ ] Hotspot classes materially shrink or lose major responsibilities.
- [ ] Guardrail tests fail on the prohibited architectural regressions.
- [ ] Final review says GO for the plugin wave with real runtime evidence.

## Proof Required

- Build/test logs from a real .NET environment.
- Guardrail test results.
- Final review report and updated spreadsheet verdict.

## Browser Validation Logging

- Capture any changed routes and at least the final plugin configuration/readiness surfaces.

## Progression Gate

- Only after SB06 closes may the external plugin wave begin.

## Suggested Agent Prompt

Implement SB06 by decomposing hotspots, adding architecture guardrail tests, and rerunning the full review in a real .NET environment. Use this subbundle as the final gate for the plugin wave.
