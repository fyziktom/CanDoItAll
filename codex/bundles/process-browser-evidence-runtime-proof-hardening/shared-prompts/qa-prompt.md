# Shared QA Prompt

```text
Validate the assigned subbundle against repo://codex/bundles/process-browser-evidence-runtime-proof-hardening.

Reject proof that only says screenshots, console logs, or browser proof exist. Required browser evidence must be durable process-visible artifacts, not just markdown links or resultSummary.evidenceRefs.

For UI/browser proof, record:
- route and actual URL
- viewport
- launch command and stop/cleanup boundary
- Playwright MCP actions
- screenshot artifact path under the scoped process run root
- snapshot/DOM/evaluate artifact path
- console artifact path
- representative interaction assertion and source of the expected behavior
- screenshot review answer: what visible product state is proved, what would make the proof shallow, and whether any required state is missing

For console proof, separate active validation errors from post-stop disconnect noise. Active JavaScript/runtime errors block acceptance. Post-stop host disconnects may be non-blocking only when the stop boundary is durable and the evidence pack does not claim the whole log is warning-free.

For critical subbundles, audit the shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note closure before passing the gate.
```
