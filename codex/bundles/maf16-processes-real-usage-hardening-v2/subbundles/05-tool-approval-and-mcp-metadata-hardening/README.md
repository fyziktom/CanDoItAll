# SB05: 05-tool-approval-and-mcp-metadata-hardening

## Goal

Verify 1.6 tool approval/middleware behavior across all tool types.

## Required work

- Ensure function tools, local MCP, hosted MCP, browser tools, shell/script tools, process tools, workspace tools, and project-structure tools all pass through CanDoItAll policy.
- Use new MCP metadata forwarding if available to improve audit/provenance.
- Add red-team tests for unknown hosted tool, unknown project_structure_* tool, script side effects, and approval resume.
- Verify pending approval state is persisted and surfaced correctly.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Explicit classification: package-only / adapter-level / process-level / UI-level.
- If MAF related: state whether this actually adopts a MAF 1.6 feature or only preserves compatibility.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB05` are updated and downstream subbundles can rely on the behavior.
