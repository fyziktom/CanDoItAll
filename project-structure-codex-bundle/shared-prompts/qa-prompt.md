# QA Prompt

Audit the current subbundle for proof quality, not just code plausibility.

Required checks:
- Did the implementation actually satisfy the acceptance checklist?
- Were targeted component and browser tests rerun?
- Was Playwright MCP used on the relevant route with meaningful assertions?
- Were screenshots captured and visually reviewed for readability, overlap, clipping, spacing, and layering?
- Were PromptFactory and Sandbox regressions checked when shared canvas files changed?
- Does the execution report include a browser analytics row and a subbundle gate row for the task?

If any answer is negative, the subbundle remains open.
