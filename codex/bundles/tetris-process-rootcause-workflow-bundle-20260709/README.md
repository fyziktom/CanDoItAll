# Process Runtime Branch-Aware QA Hardening Bundle

## Validation Summary

- Bundle preparation status: `Reopened for the 2026-07-10 persistent-repair incident`
- Bundle readiness gate: `Pending refreshed prepared-stage validation`
- Execution status: `SB00-SB14 completed; SB15-SB17 planned`
- Subbundle gate review: `SB15-SB17 pending`
- Final closure gate: `Reopened because fresh production evidence invalidated the prior retry/repair closure`
- Browser validation analytics: `Current-run snapshot, console, and screenshot proof captured by process-bound agents`

## Purpose

Prepare an implementation-ready bundle for the process runtime, dispatcher, branch routing, repair loopback, recovery advice, .NET runtime tool lifecycle, process templates, and artifact templates root-caused by GPTPro Extended in this same directory.

The Tetris process instance is the concrete incident, but this bundle is not a Tetris fix. It treats the incident as a regression fixture for a generic runtime contract problem:

- completion gates are not branch-aware;
- acceptance-only runtime/browser receipts can block repair branches;
- completion gate issues cannot route to a configured branch outcome;
- product receipt rules and capability scope receipt rules duplicate the same semantic obligation;
- generic process application code contains software-delivery and .NET domain knowledge;
- several process templates have accepted/repair branch flows without machine-readable completion issue routing;
- artifact templates and acceptance artifacts do not yet force project-structure behavior into a criteria matrix.

## Source Inputs

- GPTPro Extended analysis preserved in `bundle://00-executive-summary.md` through `bundle://09-risk-and-acceptance.md`.
- GPTPro implementation task prompts preserved in `bundle://codex-tasks/`.
- GPTPro skeleton preserved in `bundle://codex-workflow-bundle-skeleton/`.
- Incident evidence preserved in `bundle://evidence/`.
- Fresh repo inventory from `repo://Templates/Processes/processes`, `repo://src/Processes`, `repo://src/Modules/CanDoItAll.Modules.Processes`, and `repo://src/Modules/CanDoItAll.Modules.Workbench`.
- CodeAnalytics snapshot `snap-20260709103653-3a49f8a9`, scoped to process, workbench, MAF workflow, MAF executor, and unit-test projects.
- Corrective architecture request in `bundle://inputs/03-architecture-refactor-request.md`.
- Fresh CodeAnalytics snapshot `snap-20260709195146-c1b7a73e` proves the adapter still spans 20 partial files and hundreds of members; no project cycle was reported in the seven-project corrective scope.
- Final CodeAnalytics snapshot `snap-20260710022410-27d4d127` proves the corrective four-project scope has no blocking errors or dependency cycles after removing the adapter partial cluster.
- Final autonomous Tetris process `4749e033-4326-4b58-acdf-61a5cf372563` completed without operator rescue or repair escalation; root plus six child runs and all 42 agent executions completed on `gpt-5.4-mini`.
- Fresh Tetris process `7d32cae3-1dca-45e7-9014-3e7da9ffa1ae` reached `quality-repair` and exhausted five current-step repair attempts. The same visible fatal UI state persisted while incidental scaffold diagnostics changed, so whole-batch diagnostic fingerprints incorrectly appeared to show progress.
- Current-run browser console evidence identifies the concrete product defect: the generated app injects `TetrisGameState` into `Home` but does not register that service. Repair agents repeatedly edited unrelated starter scaffold files and completed while explicitly recording two unresolved browser console errors.

## Critical Path

1. Characterize the Tetris incident and similar accepted/repair template flows with failing-first tests.
2. Extract completion gate evaluation out of the partial adapter into testable generic services without behavior change.
3. Add branch-aware structured receipt rules with legacy compatibility.
4. Apply branch-aware receipt enforcement and receipt deduplication.
5. Add template-driven completion issue routing and runtime gate findings.
6. Move software-delivery/.NET recovery advice out of generic application code.
7. Migrate all impacted process templates, steps, and artifact/criteria templates.
8. Harden .NET run/stop lifecycle receipts.
9. Add observability, operator diagnostics, full regression proof, and architecture guard checks.
10. Reopen the shallow SB01 architecture result, remove the adapter partial cluster, isolate domain policy contributions, and rerun a production-path Tetris process without operator rescue.
11. Reopen retry classification so one persistent diagnostic identity cannot be hidden by unrelated diagnostic churn.
12. Move software-delivery quality repair into a typed .NET subprocess that separates manager diagnosis, mutation, independent revalidation, and one bounded bughunt handoff.
13. Prove the result across Tetris, Calculator, a work-time logger, and an SVG-heavy application without sample-specific runtime or dispatcher behavior.

## Non-Negotiable Constraints

- Do not hardcode `qa-validation`, `quality-accepted`, `repair-required`, `repair-escalation`, `Blazor`, `Tetris`, `Counter.razor`, `Weather.razor`, or `workspace_dotnet_*` in generic runtime, dispatcher, or process application logic.
- Generic code may treat branch outcome keys, issue codes, receipt purpose, and route metadata as data.
- .NET, Blazor, scaffold checks, visual-proof tool names, and software-delivery branch names belong in Workbench contributors, process templates, or domain-specific recovery advice providers.
- Missing proof because the QA step skipped its own tools is not a product repair defect.
- A deterministic product defect on an acceptance branch must be branch-routable when template metadata defines the repair branch.
- Every critical subbundle must close with artifact-backed proof under `proof/SBxx/`.
- The corrective architecture phases fail if any `AgentFrameworkProcessExecutionAdapter.*.cs` partial remains, if the adapter owns completion/subprocess/artifact policy, or if direct tests still require the adapter.
- Generic retry classification may compare structured diagnostic identities and progress history, but may not parse .NET, Blazor, UI, spreadsheet, Tetris, Calculator, file names, or software-delivery branch names.
- A repair step must not accept a known failed validation as residual risk. Domain drivers/templates must require diagnosis from the concrete failing evidence before mutation and fresh proof after mutation.

## Bundle Shape

- `inputs/` preserves the raw user request and maps GPTPro source files.
- `analysis/` records current-state evidence, assumptions, risks, and reopen triggers.
- `requirements/` normalizes root causes into implementation requirements.
- `inventories/` enumerates impacted code, process templates, artifact templates, and acceptance surfaces.
- `architecture/` contains the C# architecture gate required before implementation.
- `plan/` contains the dependency-aware phase plan and architecture checkpoints.
- `subbundles/` contains independently executable phases with strict gates.
- `reviews/` contains execution-report scaffolding and architecture self-review.

## Readiness Notes

This bundle is intentionally larger than the single blocked Tetris run. The implementation agent must audit all accepted/repair validation flows and artifact templates listed in `inventories/01-process-template-inventory.md` and `inventories/03-artifact-template-inventory.md`, then update only the surfaces that share the same failure mode.

Prepared-stage validation passed with:

- `python C:\Users\lucys\.codex\skills\candoitall-bundle-preparation\scripts\validate_bundle.py C:\repositories\CanDoItAll\codex\bundles\tetris-process-rootcause-workflow-bundle-20260709 --profile initiative --stage prepared --repo-root C:\repositories\CanDoItAll`

Final closure evidence is recorded under `bundle://proof/SB12`, `bundle://proof/SB13`, and `bundle://proof/SB14`.
