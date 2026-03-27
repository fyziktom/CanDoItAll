# Shared Implementation Prompt

```text
Implement the current feedback7 subbundle only.

Keep the work inside the existing `CanvasLib` plus `Modules.Workbench` boundaries. Do not invent a new canvas framework, command system, or modal stack.

Guardrails:
- use typed C# contracts for new node presentation data
- preserve existing preview behavior and existing command ids
- derive quick actions from current Workbench command logic instead of duplicating behavior in JavaScript
- if any reachable node type cannot satisfy the requested `Edit` behavior, document it explicitly instead of silently faking support
- if a proof expectation cannot be met, update the bundle first instead of silently narrowing scope
- final proof must include exact test commands and browser screenshots for any UI-facing subbundle
```
