# SB06: 06-tool-approval-mcp-hosted-local-policy-proof

## Goal

Prove policy coverage for function, MCP, hosted, browser, shell/script, process, and project tools.

## Required work

- Inventory all known tool types in CanDoItAll agent runtime.
- Add red-team tests for unknown hosted MCP, unknown project_structure tool, shell/script mutation, browser proof tool, process transition tool.
- Ensure every tool reaches `AgentToolInvocationPolicy` or is explicitly unavailable/guarded.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB06` are filled and the downstream dependency is safe.
