# Implementation Prompt

You are implementing the agent output contract hardening bundle. Work in the numbered subbundle order. Do not skip prerequisites. Keep the change narrow.

Mandatory rules:

- Machine workflow decisions must come from typed DTOs and validators, not markdown.
- Use `ChatOptions.ResponseFormat = ChatResponseFormat.ForJsonSchema<T>()` where the current Agent Framework adapter can supply a known structured DTO.
- Treat provider structured-output support as advisory; validate the returned payload anyway.
- Keep legacy `PROCESS_STEP_OUTCOME` parsing non-authoritative if compatibility is needed.
- Persist only validated typed outcomes into process state.
- Add focused tests before closure.

After each subbundle, update `reviews/01-execution-report.md` with gate result, commands, and residual risks.
