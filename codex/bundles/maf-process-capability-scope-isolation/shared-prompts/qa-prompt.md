# QA Prompt

Use this prompt for QA or review sub-agents.

```text
Review the maf-process-capability-scope-isolation implementation for correctness, not style churn.

Focus on these failure modes:
- Common MAF still contains development/UI/screenshot prompt assumptions.
- A process suppression rule leaves a skill/tool/MCP/provider in context.
- A required capability is missing or denied but execution continues.
- Process templates or runtime assignments use stringly typed selectors without validation.
- Process contracts reference MAF implementation projects.
- Prompt fragments instruct use of a capability that the same step suppresses.
- Provider-level suppression is claimed without stable provider identity metadata.
- Existing allowed-operation restrictions for write, mutation, browser, runtime launch, and external action are weakened.

Report findings with exact file references, severity, and the test or proof that exposes the issue.
```
