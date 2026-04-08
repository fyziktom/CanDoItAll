# Implementation Prompt

Implement only the current subbundle for `candoitall-codeanalytics-zyphonote-parity-bundle-v1`.

Rules:

- Keep `CanDoItAll.Mcp.CodeAnalytics` thin and place reusable analysis logic in `C:\repositories\CanDoItAll.CodeAnalsis`.
- Do not copy code from the sibling repo into the host repo.
- Prefer the smallest correct addition that closes the benchmark gap and improves SharpTools replacement value.
- If a new MCP tool is added, update reinstall flow and Codex-facing guidance in the same pass.
- Validate immediately after each nearby change with the smallest proof that can fail fast.

Expected outputs:

- code changes limited to the current subbundle scope
- updated bundle status and proof notes
- explicit note if a Codex restart becomes mandatory before further validation
