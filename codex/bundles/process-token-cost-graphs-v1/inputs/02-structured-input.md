# Structured Input

## Core Objective

- Correct provider-token usage, prices, and process graph analytics, then expose process and run graph views with lazy loading.

## Success Criteria

- Execution metrics persist provider input, cached input, and output tokens without successful-run prompt double counting.
- Cost calculations price uncached input, cached input, and output tokens separately.
- Live process history cost charts work after completed runs are reloaded.
- Process workspace exposes selected-process all-runs graphs and selected-run graphs with explicit lazy loading.

## Hard Constraints

- Keep usage and graph contracts strongly typed.
- Do not add fallback mechanisms that hide missing provider prices; unknown pricing should remain explicit and non-priced.
- Do not load all process-run history by default when the process graph tab is opened accidentally.
- Reuse existing chart wrappers and process workspace patterns.

## Allowed Side Effects

- AgentFramework usage response and metric aggregation contracts may change.
- Process observation analytics contracts may add scoped graph query models.
- ProcessWorkspace and related process components may add tabs and state.
- Tests and bundle proof files may be added or updated.

## Source Artifacts

- `bundle://inputs/00-original-request.md`
- `bundle://inputs/01-source-artifacts.md`

## Input Coverage Signals

| Note | Raw wording | Normalized intent | Owner |
| --- | --- | --- | --- |
| N001 | `Improve used tokens usage and price and statistic calculations.` | Fix the shared token, price, and analytics pipeline. | SB01, SB02 |
| N002 | `some process costs amounts like 0.08USD... about 100k tokens... openai billing usage... milions of tokens consumed` | Audit and correct persisted metric totals and process cost synchronization so UI costs are based on provider usage, not incomplete or duplicated estimates. | SB01 |
| N003 | `Assure we are counting also outptut and cached tokens for openai provider.` | Persist output and cached input tokens from OpenAI/Azure OpenAI usage details and price each category separately. | SB01 |
| N004 | `For example ollama, will not have cached tokens, but if provider has it we must calc it correctly.` | Preserve zero cached-token behavior for Ollama and other providers without cached usage while supporting providers that report it. | SB01 |
| N005 | `when process finished and I refreshed live processes page and selected for example 1 day history, I cannot see prices graph.` | Historical live-process analytics must include cost graph data for completed runs after refresh and one-day window selection. | SB02 |
| N006 | `on processes page there must be new tab in selected process to show graphs like we have on live processes page to show merged info about all runs of that process` | Add selected-process all-runs graph tab using the live chart model. | SB03 |
| N007 | `in specic selected process run we need also own tab for graphs for that specific process run only.` | Add selected-run graph tab scoped to one process run. | SB03 |
| N008 | `Those are lots of loading of the data, so it must load them only when that tab is selected.` | Lazy-load graph datasets only on tab activation. | SB02, SB03 |
| N009 | `For all process run there might be button "Show graphs of all runs of process"...` | Process-level graphs require an explicit load button, default one-month range, and listed range options. | SB03 |

## Dependency And Sequencing Signals

- SB01 must finish before SB02 and SB03 because all graph totals depend on persisted metrics and pricing.
- SB02 must finish before SB03 because UI should consume a scoped analytics contract rather than loading data ad hoc in the component.

## Validation Expectations

- SB01: unit/integration proof for cached input, output tokens, pricing, and no prompt double count.
- SB02: process analytics proof for completed one-day history cost series and bounded process/run scopes.
- SB03: component and browser proof for lazy graph tabs.

## Evidence Contract

- Store command transcripts under `bundle://proof/SBxx/transcripts/`.
- Store critical manifests under `bundle://proof/SB01/manifest.md` and `bundle://proof/SB02/manifest.md`.
- Store UI screenshots under `bundle://proof/SB03/browser/`.

## UI Validation Strategy

- Use a large desktop viewport first for `/processes/live` and `/processes`.
- Review screenshots for chart visibility, tab spacing, button affordance, range selector clarity, and no eager loading.
- Add narrower-width checks if the new tabs or controls wrap.

## Browser Validation Analytics

- Record route, viewport, actions, assertions, screenshots, and result in `bundle://reviews/01-execution-report.md`.

## Working Assumptions

- OpenAI/Azure OpenAI usage is available through Microsoft Agent Framework as `UsageDetails.CachedInputTokenCount`.
- Provider-reported `InputTokenCount` is the source of truth for successful runs.

## Primary Risks

- Incorrect cached-token propagation will make all downstream pricing and graph proof meaningless.
- A UI-only fix could hide empty or over-broad analytics queries.
