# Structured Input

## Objectives

- Audit all Agent Framework creation, execution, parsing, tool registration, and process-state update paths.
- Replace process-critical markdown/comment parsing with typed output contracts and validators.
- Configure structured output through the installed `Microsoft.Extensions.AI.ChatOptions.ResponseFormat` and `ChatResponseFormat.ForJsonSchema<T>()` API where supported.
- Add a finalizer-tool-ready pattern for critical decisions.
- Persist only validated typed outcomes into process runtime state.
- Add unit/integration regression tests and documentation.

## Hard Constraints

- Keep the change narrow and compatible with existing public APIs unless a contract extension is necessary.
- Do not use top-level arrays or primitives for structured output schema contracts.
- Do not silently accept malformed output.
- Do not use markdown as the machine contract.
- Keep source-code comments in English.
- Do not introduce new dependencies unless required.

## Assumptions

- The current installed Agent Framework stack is `Microsoft.Agents.AI` 1.0.0 with `Microsoft.Extensions.AI.Abstractions` 10.0.0 transitively available.
- `ChatClientAgentRunOptions` carries a `ChatOptions` instance, so structured response format can be applied through `ChatOptions.ResponseFormat` in the current adapter.
- Existing process automation can be hardened with a typed process-step outcome first; broader typed DTO families can be added incrementally where workflows start using them.

## Validation Expectations

- Bundle validator passes at prepared and completed stages.
- Focused unit/integration tests cover validators, retry/failure behavior, structured run options, and markdown non-authority.
- `dotnet build` and relevant `dotnet test` commands run, or failures are recorded with exact causes.
