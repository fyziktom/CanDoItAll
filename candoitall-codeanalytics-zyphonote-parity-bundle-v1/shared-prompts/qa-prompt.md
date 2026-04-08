# QA Prompt

Validate the current subbundle for `candoitall-codeanalytics-zyphonote-parity-bundle-v1`.

Required QA behavior:

- Use the smallest focused validation that can falsify the change quickly.
- Prefer build plus targeted tests over broad suite runs until the subbundle is stable.
- For MCP surfaces, prove the new tool works through the actual host or harness path, not just by unit-level compilation.
- Record whether the proof is strong enough for downstream subbundles, or whether the current subbundle must reopen.
- If a restart is needed before MCP validation can continue, record that as a gating condition instead of pretending the proof is complete.
