# Agent Tool Execution Integrity: Ollama and Shared Providers

Preparation is complete. **Implementation has not started.** The reported defect remains unfixed.

The reported run wrote the Markdown file, failed to bind the asset-registration call, gave the model only `Error: Function failed.`, and was nevertheless persisted as `Completed / Succeeded`. The canonical graph contains five nodes and no child under Main. Both native Ollama and OpenAI-compatible SDKs preserved the declared nested schema in a diagnostic capture.

Start with [the root-cause analysis](analysis/01-current-state.md), [requirements](requirements/01-normalized-requirements.md), and [the seven-phase plan](plan/01-phase-plan.md). [The execution report](reviews/01-execution-report.md) separates preparation proof from future implementation proof.

## Outcome Contract

- Requested outcome: investigate run `894e1404-3019-4221-8be6-7769c0f472ae` on port 5032 and prepare an implementation-ready bundle.
- Hard constraints: no product implementation in this task; retain provider-neutral tool governance; preserve authority, approval, workspace, and secret boundaries.
- Preparation evidence: live API captures, persisted tool results and receipts, source inspection, scoped CodeAnalytics, a non-mutating binary probe, and the supplied screenshot.
- Implementation completion: corrected invalid calls receive actionable safe feedback; unresolved mutations cannot look successful; scoped cross-turn evidence survives; real stored assets and automatic refresh are proved through both provider routes.
- Host: the verified 5032 process was stopped after capture. [Stop verification](analysis/host-stop.json) confirms zero listeners and no process 38720.
- Scope: CanDoItAll only. Sibling repositories were read-only. No database edits, replayed agent mutations, or model invocations were made during preparation.

## Evidence And Boundaries

The exact incident is confirmed. No shared-provider run was supplied or reproduced. The native/OpenAI SDK probe proves schema preservation and input binding, not an end-to-end shared relay or stochastic model success.

The Components MCP returned `Transport closed` on inventory and recommendation calls. Existing component source and the component skill were used to plan presentation. SB05 must re-query the MCP before markup/component changes; backend phases can proceed independently.

Existing macro architecture is mostly correct: module-owned tools execute locally above provider transport. Keep that separation. Extract only the invocation/error, completion, replay, and asset-operation responsibilities that need modification; do not replace the provider stack or refactor unrelated Workbench features.

## Bundle Layout

- `inputs/`: verbatim request, screenshot, normalized intake.
- `analysis/`: source findings, sanitized incident artifacts, schema captures, probe result.
- `architecture/`: ownership, dependency directions, patterns, testability.
- `plan/`: dependency gates, validation commands, architecture checkpoints.
- `subbundles/`: seven actionable work units, beginning with isolated MAF 1.20 upgrade characterization.
- `traceability/` and `reviews/`: input coverage, readiness, implementation status.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Pass` — see reviews/00-bundle-self-review.md for the final commands.
- Execution status: `Not started`
- Subbundle gate review: `Not started`
- Final closure gate: `Not started`
- Browser validation analytics: `Not started`; supplied screenshot inspected as incident evidence.
- Product builds/tests: not claimed. A disposable diagnostic executable used the existing Release assemblies and a fake HTTP handler.
- Portability-static: not applicable to this documentation-only change; mandatory during implementation whenever protected files change.

Preparation commands and results: [preparation validation](reviews/02-preparation-validation.md).

MAF 1.20 assessment: [why the upgrade is useful but insufficient](analysis/03-maf-1-20-assessment.md). The bundle now starts with SB00 and retains every application repair phase.
