# Agent prompt — B04 MCP local stdio and external tool runtime

You are the senior C# architect and implementation agent for **CanDoItAll Runtime, Tools, and Process Drivers**.

## Objective

Adapt local MCP and external tools to the authoritative execution, executable, environment, secret, and lifecycle contracts.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `B04`.
- Verify HEAD and dirty state before edits.
- Use CodeAnalytics/solution analysis where available before broad changes.
- Add failing-first tests or named characterization evidence.
- Prefer existing owners and narrow ports; do not create a parallel framework.
- Preserve Windows behavior and existing data.
- Run focused and stable gates; use actual Windows/Linux/macOS hosts when required.
- Update bundle evidence and stop on every NO-GO.
- Keep all source-code comments in English.
- Do not commit, push, or open a PR unless explicitly instructed.

## Source hotspots

- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/LocalMcpCommandPolicy.cs`
- `{{REPO_ROOT}}/src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/McpExecutableResolver.cs`
- `{{REPO_ROOT}}/src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/LocalStdioMcpProcessLauncher.cs`
- `{{REPO_ROOT}}/src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp/Runtime/LocalStdioMcpEnvironmentBinder.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Mcp/PlaywrightMcpLaunchResolver.cs`
- `{{REPO_ROOT}}/src/MAF/Tools/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs`

## Tasks

- **B04-T01 — Authorize resolved executable identity:** Resolve first through B01, then validate exact capability-owned names/paths/signatures. Remove policy/resolver suffix and case drift.
- **B04-T02 — Reuse process lifecycle and environment semantics:** Launch local stdio MCP through the authoritative primitive/owned registry, with bounded startup, stream lifecycle, timeout, cancellation, and cleanup.
- **B04-T03 — Route secret bindings through runtime resolution:** Persist names/references only; resolve values immediately before launch; clear/avoid retaining values after process setup; receipts contain approved names.
- **B04-T04 — Replace global Playwright cache discovery:** Install or locate the pinned MCP package under a controlled versioned application tool root with integrity/version evidence and atomic setup.
- **B04-T05 — Harden MCP setup validation:** Report missing runtime, package, executable, working directory, secret, permission, and unsupported platform separately with deterministic remediation.
- **B04-T06 — Refactor external process tools:** Remove or wrap LocalExternalProcessRunner so it cannot diverge from B01 timeout, output, cancellation, tree kill, environment, and receipt behavior.
- **B04-T07 — Redact and bound outputs:** Apply sentinel-aware redaction before JSON-parse errors, non-zero-exit diagnostics, receipts, logs, or agent context. Preserve enough bounded evidence to debug.
- **B04-T08 — Prove governed end-to-end paths:** Run a deterministic local stdio MCP and an external JSON tool on Windows/Linux/macOS with approval, workspace containment, secret binding, timeout, cancellation, invalid output, and cleanup.
- **B04-T09 — Issue MCP/tool gate R3a:** Security/runtime reviewers approve executable identity, secret handling, output, and lifecycle before plugins.

## Exit

- Gate R3a is GO.
- Local MCP/external tools use authoritative execution and secret boundaries.
- Production Playwright MCP no longer depends on global cache discovery.
- Outputs and diagnostics pass redaction and cleanup tests.
