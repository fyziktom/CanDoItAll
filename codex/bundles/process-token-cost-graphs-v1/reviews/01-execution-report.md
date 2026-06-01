# Execution Report

## Status

- Execution state: `Implemented; browser validated with disposable PostgreSQL profile`

## Outcome Check

- Requested outcome: accurate provider token/cached-token/cost accounting plus lazy process and run graph tabs.
- Current closure decision: `Code, targeted tests, completed-stage validator, and real browser screenshot proof pass.`
- Evidence still missing: `None for requested scope; default local PostgreSQL profile still needs baseline repair outside this feature.`

## Commands

- SB01 accounting builds and tests: `bundle://proof/SB01/transcripts/build-accounting-projects.txt`, `bundle://proof/SB01/transcripts/unit-provider-pricing.txt`, `bundle://proof/SB01/transcripts/integration-execution-tracking.txt`.
- SB02 processes module build and history component proof: `bundle://proof/SB02/transcripts/processes-module-build.txt`, `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt`.
- SB03 component build and lazy graph proof: `bundle://proof/SB03/transcripts/component-build.txt`, `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt`.
- Updated web host build and initial browser blocker: `bundle://proof/SB03/transcripts/web-isolated-build-and-browser-blocker.txt`.
- Browser rerun used disposable PostgreSQL database `candoitall_codex_graphs_20260601` on `http://localhost:5034`.

## Browser Artifacts

- `bundle://proof/SB02/browser/live-processes-one-day-graphs.png`
- `bundle://proof/SB03/browser/codex-process-graphs-before-load.png`
- `bundle://proof/SB03/browser/codex-process-graphs-loaded-empty.png`
- `bundle://proof/SB03/browser/process-wide-graphs-loaded.png`
- `bundle://proof/SB03/browser/process-selected-run-graphs.png`
- Historical blocker artifacts retained for audit: `bundle://proof/SB02/browser/browser-validation-blocker.md`, `bundle://proof/SB03/browser/browser-validation-blocker.md`, `bundle://proof/SB03/browser/stale-server-processes-desktop.png`.

## Subbundle Gate Results

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
| --- | --- | --- | --- | --- | --- |
| `01-01-token-usage-cost-accounting` | `Passed by prepared bundle review` | `Passed by manifest and targeted tests` | `SB02/SB03 consume persisted metrics` | `Passed` | Critical accounting foundation proved by unit and integration tests. |
| `02-02-history-analytics-data` | `Passed after SB01 tests` | `Passed with code/test and browser proof` | `SB03 consumes scoped observation query` | `Passed` | Completed priced runs, scoped graph data, and one-day Live Processes cost graph proved. |
| `03-03-process-workspace-graph-tabs` | `Passed after SB02 tests` | `Passed with code/test and browser proof` | `Final UI route browser proof captured` | `Passed` | Lazy process and run graph tabs proved by component tests and screenshots. |

## Browser Validation Analytics

| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |
| --- | --- | --- | --- | --- | --- |
| `02-02-history-analytics-data` | `/processes/live` | `1600x900` | `Range set to 1 day; Graphs tab rendered 1 observed run, 3.2K context, and Process cost chart` | `bundle://proof/SB02/browser/live-processes-one-day-graphs.png` | `Passed` |
| `03-03-process-workspace-graph-tabs` | `/processes` | `1600x900` | `Process Graphs tab first showed explicit load button, then rendered all-runs charts for 1 run after click` | `bundle://proof/SB03/browser/codex-process-graphs-before-load.png`, `bundle://proof/SB03/browser/process-wide-graphs-loaded.png` | `Passed` |
| `03-03-process-workspace-graph-tabs` | `/processes` | `1600x900` | `Selected run Graphs tab loaded only after nested tab selection and rendered selected-run charts` | `bundle://proof/SB03/browser/process-selected-run-graphs.png` | `Passed` |

## Analytics Review

- Backend, component, and browser evidence is strong enough for the accounting, history inclusion, scoped query, lazy-load behavior, and chart rendering surfaces.
- The default local PostgreSQL profile still has a baseline mismatch; browser validation used a disposable PostgreSQL database to avoid mutating that profile.
- The stale existing host remains intentionally excluded from proof for the updated UI.

## SB01 Semantic Adequacy Evidence

- Raw note owned: N001-N004, including accurate token/cost/stat calculations, output token counting, OpenAI cached token counting, and zero cached-token behavior for providers without cached usage.
- Shipped behavior: successful execution metrics now persist provider-reported input, output, cached input, and tool-call counts; successful input token metrics no longer add local prompt estimates; cached input contributes to resolved pricing when a provider price row exists.
- Source proof: `bundle://proof/SB01/transcripts/source-assertions.txt` and `bundle://proof/SB01/manifest.md`.
- Test proof: `bundle://proof/SB01/transcripts/unit-provider-pricing.txt` and `bundle://proof/SB01/transcripts/integration-execution-tracking.txt`.
- Shallow-pass trap: adding a cached-token property while leaving prompt double counting and cached-token-omitting pricing in place.
- Adversarial negative proof: `bundle://proof/SB01/semantic-invariants.md` documents the provider input 12 plus prompt-estimate case and the cached-token pricing omission case.
- Semantic positive proof: `bundle://proof/SB01/transcripts/integration-execution-tracking.txt` proves provider usage persistence; `bundle://proof/SB01/transcripts/unit-provider-pricing.txt` proves cached input contributes to price resolution.
- Anti-stub audit: no production TODO, NotImplemented, fixture-specific, test-only, or template-only markers found; see `bundle://proof/SB01/transcripts/anti-stub-audit.txt`.

- Invariant ID: SB01-I01
- Source raw note: "Improve used tokens usage and price and statistic calculations" and the observed UI/billing mismatch.
- Expected behavior: successful execution metrics persist provider input tokens without adding the local prompt estimate.
- Disallowed shallow implementation: keep the previous prompt estimate addition while adding a cached-token field.
- Failing-first test: n/a process exemption; a pre-change failing run was not captured before the one-pass repair.
- Passing test: `bundle://proof/SB01/transcripts/integration-execution-tracking.txt`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt`.
- Red-team negative case: provider input 12 plus any prompt estimate would fail because the test asserts persisted input is exactly 12.
- Downstream dependency check: SB02/SB03 read these persisted metrics for graph totals.

- Invariant ID: SB01-I03
- Source raw note: "Assure we are counting also ... cached tokens for openai provider. For example ollama, will not have cached tokens..."
- Expected behavior: cached input tokens are provider-reported and default to zero when absent.
- Disallowed shallow implementation: infer cached tokens from input tokens or provider names.
- Failing-first test: n/a process exemption; a pre-change failing run was not captured before the one-pass repair.
- Passing test: `bundle://proof/SB01/transcripts/integration-execution-tracking.txt`.
- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs`, `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`.
- Production assertions: `bundle://proof/SB01/transcripts/source-assertions.txt`.
- Red-team negative case: a provider response without cached usage remains zero instead of receiving an invented cached estimate.
- Downstream dependency check: SB02/SB03 cached-token stats and charts consume the persisted metric.

## Raw Note Closure

| Raw note | Status | Proof |
| --- | --- | --- |
| `N001` | `Solved by code/test proof` | `bundle://proof/SB01/manifest.md`, `bundle://proof/SB02/manifest.md`, `bundle://proof/SB03/manifest.md` |
| `N002` | `Solved by code/test proof` | `bundle://proof/SB01/transcripts/integration-execution-tracking.txt` |
| `N003` | `Solved by code/test proof` | `bundle://proof/SB01/transcripts/integration-execution-tracking.txt`, `bundle://proof/SB01/transcripts/unit-provider-pricing.txt` |
| `N004` | `Solved by code/test proof` | `bundle://proof/SB01/semantic-invariants.md` |
| `N005` | `Solved by code/test/browser proof` | `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt`, `bundle://proof/SB02/browser/live-processes-one-day-graphs.png` |
| `N006` | `Solved by code/test/browser proof` | `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt`, `bundle://proof/SB03/browser/process-wide-graphs-loaded.png` |
| `N007` | `Solved by code/test/browser proof` | `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt`, `bundle://proof/SB03/browser/process-selected-run-graphs.png` |
| `N008` | `Solved by code/test/browser proof` | `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt`, `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt`, `bundle://proof/SB02/browser/live-processes-one-day-graphs.png` |
| `N009` | `Solved by code/test/browser proof` | `bundle://proof/SB03/transcripts/source-assertions.txt`, `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt`, `bundle://proof/SB03/browser/codex-process-graphs-before-load.png` |

## Residual Risks

- The default local PostgreSQL profile still fails the merged baseline check; the validated host used disposable database `candoitall_codex_graphs_20260601`.
- Existing EF Core assembly conflict warnings remain unrelated to this change.
- The seeded browser run failed because Cognitive Memory context requires a project scope; that failure was useful for history/cost graph proof and is unrelated to graph rendering.
