# Original Request

The user asked on 2026-04-26 to audit and, if necessary, fix Microsoft Agent Framework usage so AI agent outputs are reliable, typed, machine-validated, and suitable for enterprise workflow automation.

Key preserved directives:

- Do not rely only on prompt instructions such as `return JSON`.
- Prefer typed structured outputs using `ResponseFormat`, `ChatResponseFormat.ForJsonSchema<T>()`, or the equivalent installed API.
- Do not use primitive or array top-level structured output contracts.
- Use finalizer function tools for critical workflow decisions where appropriate.
- Register function tools with `AIFunctionFactory.Create(...)` or the installed equivalent.
- Validate every agent output after generation.
- Use bounded repair/retry and typed failure or human escalation after retry limits.
- Persist only validated typed outputs into workflow state.
- Do not parse workflow decisions from markdown.
- Add tests and documentation.

Required initial search terms were executed against the repository with `git grep` because the bundled `rg.exe` was denied by Windows:

- `AgentRunOptions`
- `ResponseFormat`
- `ChatResponseFormat`
- `ForJsonSchema`
- `RunAsync`
- `AIFunctionFactory`
- `return JSON`
- `JSON only`
- `markdown`
- `JsonSerializer.Deserialize`
- `JObject`
- `JsonDocument`
- `JsonElement`
- `ProcessState`
- `ProcessPatch`
- `AgentStep`
- `HumanEscalation`
- `ValidationResult`
- `CodeReview`
- `ArchitectureReview`
- `ImplementationPlan`
