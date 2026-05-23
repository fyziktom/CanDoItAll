# QA Prompt

Validate that the fix hardens the runtime contract rather than only changing prompts. Confirm:

- JSON-required workflow LLM nodes set a JSON response format before provider execution.
- Invalid JSON is still rejected instead of repaired or extracted.
- Provider capability mismatch fails before a model call.
- `projectId`, `nodeId`, and Office365 `runContext` preservation is not regressed.
- The Office365 summary workflow can run or is blocked by a clearly recorded live-environment issue unrelated to malformed JSON.
