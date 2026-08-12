# Agent prompt — B01 Execution primitives, environment semantics, and executable resolution

You are the senior C# architect and implementation agent for **CanDoItAll Runtime, Tools, and Process Drivers**.

## Objective

Make one typed, OS-correct, lifecycle-owned process execution foundation before adapting Workbench, Manager, MCP, or plugins.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `B01`.
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

- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Process/LocalWorkspaceProcessHost.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandProcessRunner.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandEnvironmentPolicy.cs`
- `{{REPO_ROOT}}/src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceExecutableLocator.cs`
- `{{REPO_ROOT}}/src/MAF/Tools/CanDoItAll.AgentFramework.Tools/External/ExternalProcessToolInvoker.cs`

## Tasks

- **B01-T01 — Define the canonical execution plan:** Use immutable executable/argv/working-directory/environment/timeout/output/boundary/side-effect metadata. Display command text is a projection only.
- **B01-T02 — Consolidate low-level process semantics:** Make LocalWorkspaceProcessHost or a smaller extracted primitive authoritative. External tools and injected plugin runtimes wrap/reuse it rather than copy Process code.
- **B01-T03 — Implement host-correct executable resolution:** Handle explicit paths, PATH order, Windows PATHEXT, Unix execute bits and shebang expectations, case behavior, symlinks, missing/ambiguous candidates, and stable diagnostics.
- **B01-T04 — Implement environment semantics:** Preserve OS key comparison, define safe common and OS/tool-specific inherited sets, require explicit secret bindings, and keep values out of receipts.
- **B01-T05 — Prove cancellation and process-tree cleanup:** Characterize existing Kill(entireProcessTree). Add TERM/grace/KILL or native process-group/Job Object behavior only where tests prove it is required.
- **B01-T06 — Unify lifecycle ownership:** Ensure one process host/registry instance per workspace/runtime aggregate, one disposal path, and explicit kept-alive process leases.
- **B01-T07 — Normalize/redact receipts:** Record logical paths and approved environment names, cap stdout/stderr, redact sentinel secrets, and report actual isolation strength rather than aspirational sandbox claims.
- **B01-T08 — Remove neutral Windows suffix assumptions:** No `.exe/.cmd/.bat` probing or case-insensitive allowlist remains in OS-neutral code except explicit compatibility fixtures.
- **B01-T09 — Issue execution foundation gate R1a:** Independent runtime/security review must accept cancellation, ownership, environment, executable, and receipt behavior.

## Exit

- Gate R1a is GO for implementation on Windows/Linux actual-host tests plus deterministic macOS contract fixtures under `RUNTIME-MACOS-VALIDATION-001`; actual macOS proof remains deferred.
- One low-level process primitive and lifecycle owner are authoritative.
- Executable/environment semantics are OS-correct and security reviewed.
- No child process leak or secret-bearing receipt remains in tested paths.
