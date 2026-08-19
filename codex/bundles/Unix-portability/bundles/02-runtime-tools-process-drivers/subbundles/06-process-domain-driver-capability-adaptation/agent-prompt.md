# Agent prompt — B06 Process-domain driver and special-tool capability adaptation

You are the senior C# architect and implementation agent for **CanDoItAll Runtime, Tools, and Process Drivers**.

## Objective

Connect host capabilities to process strategies and special/domain drivers while preserving Processes as the semantic owner.

## Required reading

1. `../../../../CODEX-EXECUTION-CONTRACT.md`
2. `../../README.md`
3. this subbundle `README.md`, `tasks.md`, `validation.md`, and `exit-criteria.md`
4. `../../requirements/requirements.json`
5. `../../analysis/01-prepared-findings.md`
6. `../../inventories/source-reference-manifest.json`
7. relevant ADRs and prior gate/session handoff

## Execution instructions

- Work only on `B06`.
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

- `{{REPO_ROOT}}/src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverDescriptor.cs`
- `{{REPO_ROOT}}/src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterDescriptors.cs`
- `{{REPO_ROOT}}/src/Processes/CanDoItAll.Processes.Runtime`
- `{{REPO_ROOT}}/src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration`
- `{{REPO_ROOT}}/codex/bundles/MAF-Refactor/adrs/ADR-007-process-semantics-owned-by-processes.md`

## Tasks

- **B06-T01 — Model required host capabilities:** Let process strategies/special tools declare capabilities such as direct execution, Docker, Python, Node, pwsh script, MCP, desktop, or terminal without branching on OS.
- **B06-T02 — Keep host facts outside process semantics:** Host adapters expose availability and execution ports; Processes owns eligibility, alternate strategy, recovery, escalation, evidence, and failure interpretation.
- **B06-T03 — Validate templates/plans before side effects:** Compile or launch-check required capabilities and produce deterministic missing-capability diagnostics with safe repair/alternate strategy.
- **B06-T04 — Preserve authority and approvals:** Host capability presence never grants workspace/project scope, mutation, tool access, approval, or process authority. Re-run canonical authority and per-run workspace tests.
- **B06-T05 — Adapt special/domain drivers:** Review every driver/tool that starts processes, opens files, runs Docker/Python/Node/PowerShell/MCP, or consumes host paths. Route through B01–B05 capabilities.
- **B06-T06 — Normalize receipts/evidence:** Serialize logical paths, strategy/driver/capability IDs, tested platform profile, and bounded diagnostics; omit secrets and unnecessary host absolute paths.
- **B06-T07 — Define platform layer semantics:** Document that ProcessDriverLayer.Platform means a process strategy package constrained by host capabilities, not a generic operating-system service layer.
- **B06-T08 — Test alternate and unavailable paths:** For each special tool, prove success on supported profiles and planned fail/alternate behavior elsewhere without accidental escalation loops.
- **B06-T09 — Issue process architecture gate R3:** Independent process/MAF/security review confirms ownership, authority, capability, receipts, and recovery.

## Exit

- Gate R3 is GO.
- Processes remains the semantic owner and MAF remains a generic execution adapter.
- Every special/domain driver declares and consumes host capabilities through approved boundaries.
- Unsupported profiles fail or choose alternatives deterministically before unsafe side effects.
