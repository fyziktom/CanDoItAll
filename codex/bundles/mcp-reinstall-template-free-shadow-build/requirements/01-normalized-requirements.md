# Normalized Requirements

| ID | Requirement | Source notes | Owning subbundle | Proof |
| --- | --- | --- | --- | --- |
| REQ-001 | Keep repository templates under `C:\repositories\CanDoItAll\Templates` and do not remove their shared build support for workflows that opt in. | NOTE-001 | SB01 | Source diff and build-target assertion. |
| REQ-002 | MCP reinstall and DotNetWatch shadow preparation must disable repository template copying for MCP builds/publishes. | NOTE-002, NOTE-005 | SB01 | Source assertion plus no `Templates` directories under MCP artifacts after reinstall. |
| REQ-003 | DotNetWatch shadow preparation must build the project through a standard repo Release output, then copy the final output directory into `.artifacts\mcp-server-shadow`. | NOTE-004, NOTE-006 | SB01 | Wrapper transcript and manifest path assertion. |
| REQ-004 | Reinstall must continue preparing DotNetWatch, installing Components, CodeAnalytics, SshOps, Manager, Tray, updating config/manifest, and syncing repo-managed skills unless explicitly skipped by existing switches. | NOTE-003 | SB01 | Passing full reinstall transcript and install manifest assertion. |
| REQ-005 | The fix must be validated through the script path that failed, not only through isolated compilation. | NOTE-007 | SB01 | Passing `tools\Reinstall-CanDoItAllMcps.ps1` transcript. |
