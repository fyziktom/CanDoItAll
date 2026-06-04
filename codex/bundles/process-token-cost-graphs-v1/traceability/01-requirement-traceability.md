# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| N001 / R001 / R003 / R007 | `requirements/01-normalized-requirements.md` | `subbundles/01-01-token-usage-cost-accounting`, `subbundles/02-02-history-analytics-data` | Provider pricing tests, execution-run tracking tests, process analytics tests | Shared accounting and graph-statistics foundation. |
| N002 / R001 | `analysis/01-current-state.md` | `subbundles/01-01-token-usage-cost-accounting` | Execution-run metric test proving provider input is not inflated by prompt estimate | Addresses observed UI/billing mismatch root cause inside local accounting. |
| N003 / R002 / R003 | `architecture/01-target-solution.md` | `subbundles/01-01-token-usage-cost-accounting` | MAF/runtime fixture or integration test with cached input tokens and pricing assertion | OpenAI cached input support. |
| N004 / R002 / R003 | `architecture/01-target-solution.md` | `subbundles/01-01-token-usage-cost-accounting` | Provider pricing tests proving zero cached tokens remain valid for non-cached providers | Do not infer cached tokens for Ollama-style providers. |
| N005 / R004 | `requirements/01-normalized-requirements.md` | `subbundles/02-02-history-analytics-data` | Process observation test with completed priced run in one-day window and non-empty money series | Fixes missing price graph after refresh. |
| N006 / R005 / R007 / R008 | `requirements/01-normalized-requirements.md` | `subbundles/03-03-process-workspace-graph-tabs` | Component and browser proof for selected-process graph tab, range selector, and all-runs data load button | Must be lazy and scoped. |
| N007 / R006 / R007 / R008 | `requirements/01-normalized-requirements.md` | `subbundles/03-03-process-workspace-graph-tabs` | Component and browser proof for selected-run graph tab scoped to one run | Depends on SB02 scoped query. |
| N008 / R005 / R006 / R008 | `inputs/02-structured-input.md` | `subbundles/02-02-history-analytics-data`, `subbundles/03-03-process-workspace-graph-tabs` | Query-scope tests and component tests proving no eager load | Performance constraint. |
| N009 / R005 / R008 | `inputs/02-structured-input.md` | `subbundles/03-03-process-workspace-graph-tabs` | Component/browser proof for explicit button, default one-month range, and all required range options | All-runs graph UX requirement. |
