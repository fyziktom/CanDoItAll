# SB14 - Regression Proof Cleanup And Docs

## Status

- `Completed`

## Objective

Close the initiative by running final regression proof, removing obsolete workflow/executor paths, updating documentation and developer conventions, and validating the completed bundle with artifact-backed evidence.

## Success Criteria

- Obsolete workflow/executor code, registrations, and tests from old locations are removed after SB13 proves no fallback dependency remains.
- Final build/test/browser proof covers workflow runtime, default executors, plugin executors, templates, MAF adapter, API, Blazor UI, Workbench workflow nodes, and process integration.
- Documentation explains the new workflow/executor project boundaries and how to add workflows, templates, default executors, and plugin executors.
- Final regression proves typed failure diagnostics, repair hints, redaction, retryability, and no-generic-error behavior across runtime, default executors, plugins, templates, MAF adapter, API, UI, Workbench, and external tool/MCP paths.
- Final documentation explains diagnostic and file-responsibility rules for future workflow/executor/plugin additions.
- Completed-stage bundle validator passes.

## Covered Inputs

- R01-R18.
- Full raw request closure, including base-up execution, plugin consequences, XLSX mapping, performance review, and hardening checkpoints.

## Prerequisites

- SB13 passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src`
- `C:\repositories\CanDoItAll\tests`
- `C:\repositories\CanDoItAll\Templates\Workflows`
- `C:\repositories\CanDoItAll\codex\bundles\workflow-node-project-isolation`
- `C:\repositories\CanDoItAll\CanDoItAll.slnx`

## Deliverables

- Final cleanup of obsolete old workflow/executor implementation paths and registrations.
- Final regression suite transcript.
- Developer documentation for workflow projects, executor projects, category ownership, plugin executor adapters, template loading, and hardening rules.
- Developer documentation for failure diagnostics, redaction, retryability, repair hints, no-generic-error requirements, and file-size/responsibility expectations.
- Updated workbook with final status/proof links.
- Completed execution report and proof manifest.
- Completed-stage validator pass.

## Dependency Impact

- This is the closure phase. It must leave the repository in a maintainable state where future workflow and executor work has obvious homes, tests, and boundary rules. Weak closure creates long-term confusion and makes the isolation easy to regress.

## Validation Depth

- `End-to-end regression and closure`
- Full planned unit/integration/component/browser subset, architecture checks, performance review, documentation review, and completed-stage validator proof.

## Implementation Steps

1. Review SB13 no-fallback proof and delete obsolete source only when references are proven gone.
2. Run full planned build/test subsets across workflow, executor, plugin, MAF adapter, API, UI, Workbench, and process integration areas.
3. Run browser regression for workflow page and Workbench workflow-node path.
4. Run final architecture checks for dependency direction and old path absence.
5. Run final no-generic-error and redaction audit across workflow/runtime/executor/plugin/template/MAF/API/UI/Workbench paths.
6. Run final focused performance scan and document unresolved accepted risks.
7. Run final file-size/responsibility review and document approved exceptions.
8. Update docs explaining project ownership, extension patterns, diagnostic contracts, and helper split rules.
9. Update workbook final statuses and proof links.
10. Run completed-stage bundle validator and repair structural or proof gaps.

## Scope Exceptions

- New workflow features, new executor capabilities, and plugin marketplace changes are out of scope unless required to fix regressions introduced by the isolation.

## Do Not Do

- Do not delete code without reference/proof review.
- Do not leave TODO-only documentation in place of actual ownership guidance.
- Do not close the bundle with skipped browser proof if UI routes changed.
- Do not claim performance closure without recording scan results.
- Do not close with generic workflow/executor/plugin/tool failures or undocumented diagnostic exceptions.
- Do not close with copied monoliths in new projects unless an exception is explicitly justified with owner and follow-up.

## Acceptance Checklist

- [x] Obsolete paths are removed or explicitly justified.
- [x] Final test suite passes or any failures are documented as unrelated with evidence.
- [x] Browser proof passes for workflow and Workbench routes.
- [x] Documentation covers workflows, executors, templates, plugins, and hardening expectations.
- [x] Documentation covers typed diagnostics, redaction, retryability, repair hints, and no-generic-error rules.
- [x] File-size/responsibility review passes or approved exceptions are documented.
- [x] Workbook is updated with final status and proof links.
- [x] Completed-stage validator passes.
- [x] Raw request traceability is fully closed.

## Proof Required

- `proof/SB14/manifest.md` with final changed file hashes, build/test transcripts, browser screenshots, architecture check transcript, performance scan transcript, documentation diff summary, workbook path, and validator transcript.
- `proof/SB14/semantic-invariants.md` covering full request closure, no fallback, stable compatibility, plugin behavior, template behavior, UI/API/Workbench behavior, typed diagnostics, redaction, repair hints, file responsibility, performance risk disposition, and anti-stub audit.
- Completed bundle proof manifest under `proof/final/manifest.md`.
- Semantic Adequacy Gate proof with raw-note literal closure and artifact-backed proof for every requirement R01-R18.

## Browser Validation Logging

- Required routes:
  - Workflow page route used by `WorkflowsPage`.
  - Workbench project-structure page route with workflow-node interaction.
- Required viewport passes:
  - Maximized large-screen pass only.
  - Small and medium viewport tests are intentionally skipped because the app is large-screen-only for this initiative.
- Required Playwright actions:
  - Load workflow templates.
  - Inspect or create workflow nodes.
  - Verify default executor display.
  - Verify plugin executor metadata display with fixture/plugin data when available.
  - Verify failed workflow/executor/plugin/template diagnostics show repair context and mask sensitive details.
  - Run Workbench workflow-node scenario.
  - Capture console errors and screenshots.
- Evidence:
  - Screenshots under `proof/SB14/browser/`.
  - DOM assertion transcript.
  - Console/network summary.
- Review questions:
  - Are workflow and Workbench pages usable after cleanup?
  - Are executor/plugin labels and statuses accurate?
  - Did cleanup remove any visible behavior?

## Progression Gate

- The initiative is not complete until SB14 passes final regression proof, workbook update, documentation review, and completed-stage validator. If any raw requirement remains unclosed, reopen the owning subbundle instead of closing with a summary-only claim.

## Suggested Agent Prompt

```text
Implement SB14 only. Close the workflow-node project isolation initiative after SB13 passes. Remove obsolete paths only with no-fallback proof, run final regression/browser/architecture/diagnostic/performance validation, update docs and workbook, run the completed-stage validator, and close every raw requirement with artifact-backed proof.
```
