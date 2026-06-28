# Shared QA Prompt

```text
Review the assigned subbundle in code-review posture. Lead with bugs, regression risk, missing proof, and boundary violations.

Check whether the implementation preserves existing capability keys, runtime tool names, seed behavior, MAF runtime behavior, process/workflow tool policy, and process/workflow capability restrictions. Reject proof that relies on old hardcoded fallback paths. Reject hidden skill/tool/MCP suppression outside the shared effective capability set. Reject generic external tool/MCP failure messages when structured diagnostic fields are available. For UI/API work, require component tests and Playwright evidence for setup, access-policy preview, failed setup diagnostics, and test flows.

For critical and checkpoint subbundles, verify proof/SBxx/semantic-invariants.md and proof/SBxx/manifest.md contain failing-first evidence, passing evidence, anti-stub checks, static/performance scan summaries where required, and artifact paths that can be independently inspected.
```
