# Assumptions And Risks

## Assumptions

- `Microsoft.Extensions.AI.ChatOptions.ResponseFormat` is the installed structured-output mechanism for this repository.
- `ChatResponseFormat.ForJsonSchema<T>()` is available through `Microsoft.Extensions.AI.Abstractions` 10.0.0.
- Existing process automation tests are the best regression surface for workflow decisions.
- The first minimal correct fix is a typed process-step outcome contract and validator, then a reusable runner extension for structured outputs.

## Critical Path Risks

- If structured output is added only to prompts and not to `ChatOptions.ResponseFormat`, the architecture remains unsafe.
- If process dispatch keeps accepting the HTML comment as authoritative, branch decisions remain markdown-driven.
- If validators only check JSON syntax, invalid business states can still be persisted.

## Validation Risks

- Full-solution tests may be expensive or environment-sensitive because the repo includes integration, MCP, Playwright, and PostgreSQL-related tests.
- Provider-specific structured output behavior cannot be proven without live credentials; unit/integration tests must prove option wiring and validation behavior locally.
- Some existing tests assert `PROCESS_STEP_OUTCOME`; they will need migration or compatibility assertions around non-authoritative legacy parsing.

## Reopen Triggers

- Any process step can still complete solely from assistant markdown without typed validation.
- Any branch outcome can still be selected from unvalidated text.
- A structured run can persist malformed JSON as success.
- A provider ignores `ResponseFormat` and the wrapper fails to catch or repair the invalid output.
- Tests pass only by preserving the legacy HTML-comment path as the source of truth.
