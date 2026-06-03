# SB02 Browser Validation Blocker

SB02 changed browser-tool capability metadata and process operation requirements. A minimal in-app browser validation route was attempted so the proof could include browser navigation, snapshot, click/type, screenshot, and console evidence.

Attempts:

- `data:text/html` route for an inline proof page.
- `file:///C:/repositories/CanDoItAll/codex/bundles/process-workflow-agent-hardening-v2/proof/SB02/browser/sb02-browser-policy-proof.html`.

Both attempts were rejected by the Browser Use URL policy before navigation. The browser tool response stated that the requested page URL was blocked and that the agent must not attempt to achieve the same outcome through workaround or alternate browser surfaces.

Decision:

- No additional browser route attempts were made.
- Executable browser-tool policy validation remains covered by unit tests in `bundle://proof/SB02/transcripts/passing-agent-tool-policy-tests.txt`, including `EvaluateAsync_SB04_INV_001_denies_browser_tools_without_capture_runtime_proof_operation`, `EvaluateAsync_SB04_INV_002_allows_browser_tools_with_capture_runtime_proof_operation`, `EvaluateAsync_denies_unbounded_governed_browser_snapshot`, and `EvaluateAsync_denies_full_page_governed_browser_screenshot`.
