# Processes UI decoupling PC1

**One substantial implementation assignment for Codex GPT-6 Astra Max.**

Complete the standalone Processes workspace and Live Processes rendering boundary, including authoring, canvas, roles/steps/templates, runtime views, files, cancellation and manager chat/voice. Preserve current production functionality and the completed Workbench execution integration. Do not stop after an initial fix or a few extracted panels.

Read [prompt.md](prompt.md), then [review](REVIEW.md), [architecture](ARCHITECTURE.md), [stages](EXECUTION_STAGES.md), [feature contract](FEATURE_PARITY.md), [regressions](S0_REGRESSIONS.md), [validation](VALIDATION_MATRIX.md), [browser journeys](UI_JOURNEYS.md) and [performance/delivery](DEPENDENCIES_AND_PERFORMANCE.md). Use the active [shared v4 companion](../UI_Decoupling_Shared_Bundle/README.md), not an embedded historical v3 copy.

The observed branch was `components-decoupling` at `8549e6a18a22638d595bc76ba4240a61ba70351d`, merged from `development` on 2026-10-08. Work on the actual supplied checkout and refresh drift; do not reset to that revision. The [source register](SOURCES.json) distinguishes complete/partial reads and metadata-only dependencies. All 31 older assignment entry briefs were reviewed in the shared [history](../UI_Decoupling_Shared_Bundle/audit/bundle-history.md); their pending/pass labels are not this run's results.

## Scope and proof

There are eight inspected/inventoried native renderer families and three route-host files, not eight independent assignments. Existing Process Contracts/Projections may already supply much of the light model closure. Evaluate and reuse them before adding another DTO/project layer.

Required proof combines light tests, real native owner/consumer tests, an independent full-surface sandbox, actual production browser actions, asset/dependency closure, measured development-loop observations and the current mandatory closure gates. All supplied product-evidence groups start `NOT_RUN`. This package contains no product implementation, executed .NET result or browser evidence.

## Install and start

Place this directory and `UI_Decoupling_Shared_Bundle` beside each other under `codex/bundles`. Upgrade only the active shared directory using its migration procedure. Do not recursively copy, rewrite or execute historical packages.

```text
Read and execute codex/bundles/CanDoItAll_Processes_UI_Decoupling_PC1/prompt.md
with codex/bundles/UI_Decoupling_Shared_Bundle.
Implement the complete assigned scope and current validation, not another plan.
```

Package verification from this directory:

```text
python -B ../UI_Decoupling_Shared_Bundle/tools/handoff_tools.py verify . --shared ../UI_Decoupling_Shared_Bundle
```

See [package-only validation](PACKAGE_VALIDATION.md) for executed handoff checks. This verifies the handoff, not CanDoItAll. Keep actual evidence in a separate task-owned directory. Everything authored in the repository is English; the final human explanation may be Czech.
