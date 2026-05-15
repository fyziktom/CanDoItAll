# Implementation Prompt

Implement the selected subbundle only.

Before editing, read the root README, phase plan, traceability table, workbook checklist rows for the subbundle, and the selected subbundle README. Confirm prerequisites, exact source references, and the entry gate. Make the smallest correct change set that reduces page length or helper density while preserving route behavior, test ids, events, state ownership, and service orchestration boundaries.

Helper extraction rules:

- Move pure or mostly pure logic into internal static helpers with strongly typed signatures.
- Do not move service calls or stateful lifecycle behavior into helpers.
- Add focused helper tests when extracted branching behavior is not already covered.

Component extraction rules:

- Extract one visible region at a time.
- Use typed parameters and explicit callbacks.
- Preserve BaseLib and CanvasLib wrappers.
- Retry the CanDoItAll components MCP before introducing new structural layout markup; if unavailable, inspect local shared component usage and record the fallback.

After implementation, run the proof listed by the subbundle, update `reviews/01-execution-report.md`, update the subbundle status, and stop if the progression gate cannot honestly pass.
