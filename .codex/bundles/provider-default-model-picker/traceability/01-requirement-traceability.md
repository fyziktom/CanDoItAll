# Requirement Traceability

| Raw input | Normalized requirements | Impacted surface | Proof method | Owner |
| --- | --- | --- | --- | --- |
| Provider selection must offer default model. | R001, R002 | Agent Runtime tab | bUnit agent dialog test, browser Runtime tab proof | 02 |
| Dropdown must include default plus allowed models. | R001, R003 | Shared selector, Agent Runtime tab, workflow candidate | Component test, rendered markup inspection | 01, 02 |
| OpenAI known names and Ollama available models should appear. | R003 | Provider model options | Tests seed provider `SuggestedModels`; code relies on provider profile `SuggestedModels`. | 01 |
| Override checkbox and standard text field must remain. | R004 | Shared selector, Agent Runtime tab | Component test and existing Playwright flow update | 01, 02 |
| Generic component because providers are used in workflows, memory, etc. | R005, R006 | AgentFramework shared components, workflow/memory review | Source diff and execution report notes | 01, 02 |

## Literal Language Preservation

- "must offer its default model" is preserved as R001.
- "default option is provider default model" is preserved as R001 and R002.
- "options that specified model allows" is implemented through provider `SuggestedModels` rather than a new hard-coded list.
- "also checkbox override model name" is preserved as R004.
- "generic component" is preserved as R005.
