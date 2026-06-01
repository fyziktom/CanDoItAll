# Execution Report

## Status

- Execution state: `Implemented; browser validation blocked by local PostgreSQL baseline`

## Outcome Check

- Requested outcome: accurate provider token/cached-token/cost accounting plus lazy process and run graph tabs.
- Current closure decision: `Code and targeted tests pass; real browser screenshot proof is blocked by local database baseline mismatch.`
- Evidence still missing: updated-route browser screenshots and final completed-stage validator pass.

## Commands

- SB01 accounting builds and tests: `bundle://proof/SB01/transcripts/build-accounting-projects.txt`, `bundle://proof/SB01/transcripts/unit-provider-pricing.txt`, `bundle://proof/SB01/transcripts/integration-execution-tracking.txt`.
- SB02 processes module build and history component proof: `bundle://proof/SB02/transcripts/processes-module-build.txt`, `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt`.
- SB03 component build and lazy graph proof: `bundle://proof/SB03/transcripts/component-build.txt`, `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt`.
- Updated web host build and browser blocker: `bundle://proof/SB03/transcripts/web-isolated-build-and-browser-blocker.txt`.

## Browser Artifacts

- `bundle://proof/SB02/browser/browser-validation-blocker.md`
- `bundle://proof/SB03/browser/browser-validation-blocker.md`
- `bundle://proof/SB03/browser/stale-server-processes-desktop.png`
- `bundle://proof/SB03/browser/stale-server-startup-dialog-snapshot.md`
- `bundle://proof/SB03/browser/stale-server-after-continue-snapshot.md`
- `bundle://proof/SB03/browser/stale-server-main-depth8.md`

## Subbundle Gate Results

| Subbundle | Entry gate | Closure gate | Downstream dependencies checked | Progression result | Notes |
| --- | --- | --- | --- | --- | --- |
| `01-01-token-usage-cost-accounting` | `Passed by prepared bundle review` | `Passed by manifest and targeted tests` | `SB02/SB03 consume persisted metrics` | `Passed` | Critical accounting foundation proved by unit and integration tests. |
| `02-02-history-analytics-data` | `Passed after SB01 tests` | `Passed for code/test proof; browser blocked` | `SB03 consumes scoped observation query` | `Passed with browser blocker` | Completed priced runs and scoped graph data proved by component tests. |
| `03-03-process-workspace-graph-tabs` | `Passed after SB02 tests` | `Passed for code/test proof; browser blocked` | `Final UI route browser proof blocked` | `Passed with browser blocker` | Lazy process and run graph tabs proved by component tests. |

## Browser Validation Analytics

| Subbundle | Route | Viewport | Playwright MCP evidence | Screenshots | Result |
| --- | --- | --- | --- | --- | --- |
| `02-02-history-analytics-data` | `processes live route` | `1600x900` | `Fresh updated host blocked before route load by PostgreSQL baseline mismatch` | `bundle://proof/SB02/browser/browser-validation-blocker.md` | `Blocked` |
| `03-03-process-workspace-graph-tabs` | `processes route` | `1600x900` | `Existing host loaded stale build; isolated updated host blocked before route load by PostgreSQL baseline mismatch` | `bundle://proof/SB03/browser/browser-validation-blocker.md` | `Blocked` |

## Analytics Review

- Backend and component evidence is strong enough for the accounting, history inclusion, scoped query, and lazy-load behaviors.
- Browser evidence is not strong enough to visually close the UI route because the updated web host cannot start against the current local PostgreSQL profile.
- The stale existing host was intentionally not used as proof for the updated UI.
- Subbundle progression proceeded because component tests directly proved the requested lazy-load and scoped graph behavior, but final visual proof should be rerun after database baseline repair or a disposable database profile is available.

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
| `N005` | `Solved by code/test proof; browser blocked` | `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt`, `bundle://proof/SB02/browser/browser-validation-blocker.md` |
| `N006` | `Solved by code/test proof; browser blocked` | `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt`, `bundle://proof/SB03/browser/browser-validation-blocker.md` |
| `N007` | `Solved by code/test proof; browser blocked` | `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt`, `bundle://proof/SB03/browser/browser-validation-blocker.md` |
| `N008` | `Solved by code/test proof; browser blocked` | `bundle://proof/SB02/transcripts/component-history-and-scope-tests.txt`, `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt` |
| `N009` | `Solved by code/test proof; browser blocked` | `bundle://proof/SB03/transcripts/source-assertions.txt`, `bundle://proof/SB03/transcripts/component-lazy-graph-tests.txt` |

## Residual Risks

- Real browser visual proof is blocked until the local PostgreSQL profile matches the merged baseline or a disposable profile is available.
- Existing EF Core assembly conflict warnings remain unrelated to this change.
- The already-running local app was stale during validation and was not stopped.
