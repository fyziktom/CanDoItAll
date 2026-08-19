# SB01 proof manifest

CP0 passed at repository head `bca2c286d32c48ba0283a8f606f6cc5c8639afca` with no production changes.

- Source, skill hashes, architecture, dependency, consumer, CSS/DOM, owner-test, UI, Components MCP, and semantic-invariant evidence is present in this directory.
- Debug Web build: passed with 0 warnings and 0 errors.
- Focused test discovery: expected 1, actual 1.
- Focused test run: passed 1/1.
- Boundary baseline: passed; the default missing-neutral-project result is the expected SB01/SB02 phase boundary.
- Production UI exclusion scan: no Agent UI reference to Simple Chat or `Modules.LlmChats`.
- Playwright large-desktop baseline: inspected catalog, main chat, floating catalog, floating lifecycle settings, and Agent settings; console clean.

Machine-readable artifact hashes are in `manifest.json`.

