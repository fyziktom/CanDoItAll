# Assumptions And Risks

## Assumptions

- OpenAI and Azure OpenAI usage reaches the runtime through `UsageDetails.CachedInputTokenCount`.
- Provider-reported input/output/cached-token usage is authoritative for successful provider calls. Local token estimates are acceptable only for no-response failure metrics or UI-only estimates where the provider did not return usage.
- Providers such as Ollama that do not report cached input tokens should persist `0` cached input tokens, not an inferred value.
- Existing chart wrappers can represent the requested graph data without introducing a new charting dependency.
- No reconciliation against the external OpenAI billing API is required in this bundle.

## Critical Path Risks

- SB01 is the critical foundation. If cached tokens are not propagated through runtime response, aggregation, metric persistence, and pricing, every downstream graph and process-cost result will be wrong.
- SB02 is the data foundation for SB03. If scoped analytics queries are empty, unbounded, or only valid for live runs, UI tabs will either show misleading charts or cause expensive queries.
- SB03 depends on lazy-load state being explicit. Eager loading all runs on accidental tab selection would violate the user’s performance constraint.

## Validation Risks

- Browser proof may require seeded process data or an existing local app state with completed priced runs. If the environment lacks data, component/integration tests must carry the behavioral proof and the execution report must state the browser limitation.
- Billing mismatches can only be reduced by correcting local accounting. They cannot be fully proven without provider-side usage exports, which are out of scope.
- UI tests may validate lazy loading more reliably than screenshots; screenshots still need to prove layout and user-visible graph surfaces.

## Reopen Triggers

- Reopen SB01 if any successful run still adds prompt estimates to provider input usage, cached input remains zero for an OpenAI fixture that reports cached tokens, or process cost ignores cached token pricing.
- Reopen SB02 if one-day completed-run history produces empty money series despite priced metrics in the selected window.
- Reopen SB03 if the selected-process all-runs graphs load before the explicit button click, or selected-run graphs include other runs.
