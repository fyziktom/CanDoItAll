# SB13 - Adoption Refactoring Hardening Checkpoint

## Status

- `Completed`

## Objective

Force a refactoring-hardening checkpoint after MAF adapter, API, Blazor UI, and Workbench adoption. This checkpoint must prove the isolated architecture is actually being consumed and no hidden fallback or old ownership path remains before final cleanup.

## Success Criteria

- API, UI, Workbench, host composition, templates, runtime, and executor catalog all consume isolated workflow/executor projects.
- Architecture checks reject old references to MAF workflow internals from API/UI/Workbench/template/core paths.
- Browser, component, and service tests prove adopted behavior is stable.
- Diagnostics and performance findings from adoption are fixed or explicitly assigned to final closure.
- API/UI/Workbench diagnostic display is repairable, typed, redacted, and not reconstructed from exception strings.
- Adoption did not create new oversized API/UI/service files or move old mixed responsibilities into new locations.

## Covered Inputs

- R11, R12, R13, R14, R15, R17, R18.
- Architect note requiring checkpoint hardening after logical blocks.

## Prerequisites

- SB12 completed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Hosting\AgentFrameworkServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\WorkflowsApi.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Plugins`
- `C:\repositories\CanDoItAll\src\plugins`
- `C:\repositories\CanDoItAll\tests`

## Deliverables

- Adoption hardening report.
- Architecture and no-fallback tests for API/UI/Workbench/host references.
- Focused browser rerun for affected workflow and Workbench routes.
- Focused performance scan for adoption hot paths, descriptor materialization, template loading, and UI data shaping.
- No-generic-error audit for API/UI/Workbench/template/MAF adapter failure display.
- File-size/responsibility review for adopted API/UI/service code.
- Cleanup changes limited to adoption defects discovered by the checkpoint.
- Updated execution report gate status.

## Dependency Impact

- SB14 final cleanup depends on this checkpoint proving that the new architecture is live. Without SB13, final cleanup can delete or keep the wrong paths and mask regressions behind leftover registrations.

## Validation Depth

- `Critical adoption hardening`
- Architecture, unit, integration, component, browser, diagnostics, and performance proof.

## Implementation Steps

1. Run full focused build/test subsets for workflow, executor, plugin, MAF adapter, API, UI, and Workbench projects.
2. Run architecture/no-fallback checks for old MAF workflow paths and forbidden references.
3. Re-run browser validation on workflow and Workbench routes affected in SB12.
4. Run focused performance scan on template loading, descriptor aggregation, executor display adaptation, and workflow page state shaping.
5. Run no-generic-error audit against API responses, UI state, event feed display, and Workbench diagnostics.
6. Run file-size/responsibility review for adoption code and split helper services when UI/API classes absorb non-trivial logic.
7. Review logs/errors for actionable diagnostics, repair hints, retryability, and sensitive-data masking.
8. Fix only adoption-scope defects.
9. Update proof manifests, semantic invariants, workbook, and execution report.

## Scope Exceptions

- Removing obsolete files and final docs is SB14.
- New feature work is out of scope.

## Do Not Do

- Do not delete old source paths until the no-fallback proof identifies them as unused.
- Do not broaden UI refactoring beyond adoption defects.
- Do not waive browser proof when visible routes changed.
- Do not pass the checkpoint if failed workflow/plugin/tool states are generic or lack repair context.

## Acceptance Checklist

- [x] API/UI/Workbench/host references use isolated services.
- [x] No-fallback architecture checks pass.
- [x] Component and browser validation passes.
- [x] Performance findings are fixed or assigned.
- [x] API/UI/Workbench failure display shows typed, redacted, repairable diagnostics.
- [x] Adoption files pass responsibility/file-size review or have approved helper/service splits.
- [x] Execution report marks SB13 as passed before SB14 starts.

## Proof Required

- `proof/SB13/manifest.md` with build/test transcripts, architecture/no-fallback transcript, browser screenshots, console logs, performance scan transcript, and changed file hashes.
- `proof/SB13/semantic-invariants.md` covering live adoption, no old fallback, UI/API compatibility, Workbench compatibility, typed diagnostics, redaction, repair hints, file responsibility, and performance-risk disposition.
- Semantic Adequacy Gate proof with adversarial old-reference/fallback cases, positive UI/API/Workbench cases, and anti-stub audit.

## Completion Evidence

- Proof manifest: `bundle://proof/SB13/manifest.md`.
- Semantic invariants: `bundle://proof/SB13/semantic-invariants.md`.
- Guard tests: `bundle://proof/SB13/transcripts/focused-adoption-hardening-tests.txt` passed 5/5.
- Combined hardening tests: `bundle://proof/SB13/transcripts/combined-hardening-unit-tests.txt` passed 37/37.
- Component proof: `bundle://proof/SB13/transcripts/component-workflows-page-tests.txt` passed 21/21.
- Integration proof: `bundle://proof/SB13/transcripts/integration-adoption-smoke-tests.txt` passed 46/46.
- Browser proof: `bundle://proof/SB13/transcripts/playwright-workflow-shell-large.txt` and `bundle://proof/SB13/transcripts/playwright-workbench-workflow-node-large.txt` each passed 1/1 with screenshots under `bundle://proof/SB13/browser/`.
- Static proof: `bundle://proof/SB13/transcripts/architecture-no-fallback-check.txt`, `bundle://proof/SB13/transcripts/no-generic-error-audit.txt`, `bundle://proof/SB13/transcripts/anti-stub-audit.txt`, `bundle://proof/SB13/transcripts/file-size-responsibility-review.txt`, and `bundle://proof/SB13/transcripts/performance-scan.txt`.

## Browser Validation Logging

- Required routes:
  - Workflow page route used by `WorkflowsPage`.
  - Workbench project-structure page route with workflow-node interaction.
- Required viewport passes:
  - Maximized large-screen pass only.
  - Small and medium viewport tests are intentionally skipped because the app is large-screen-only for this initiative.
- Required Playwright actions:
  - Repeat SB12 positive path.
  - Verify no console errors.
  - Verify executor/plugin display still renders through isolated services.
- Evidence:
  - Screenshots under `proof/SB13/browser/`.
  - DOM assertion transcript.
  - Console/network error summary.
- Review questions:
  - Are all workflow controls usable?
  - Did adoption introduce overlapping text or layout shifts?
  - Is stale/fallback UI state absent after refresh?

## Progression Gate

- SB14 cannot start until SB13 proves live adoption and no hidden fallback path remains. Any old MAF workflow reference must be removed, justified as adapter-only, or assigned to SB14 cleanup with proof.

## Suggested Agent Prompt

```text
Implement SB13 only. Harden the adoption completed in SB11-SB12. Run focused tests, architecture/no-fallback checks, browser proof, no-generic-error diagnostics review, file-size/responsibility review, and performance scans. Fix only adoption-scope defects and record Semantic Adequacy Gate proof. Do not perform final cleanup until the gate passes.
```
