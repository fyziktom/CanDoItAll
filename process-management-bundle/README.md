# CanDoItAll Process Management Bundle

This bundle captured the original process-management delivery, the `2026-04-09` reopen audit, the follow-on remediation needed to close the architecture, realistic-seed, and process-canvas parity gaps, and the completed phase07 delivery for process-definition MCP access.

The legacy architect material remains preserved under `00-context` through `05-manifest`, while the validator-compatible bundle structure and execution evidence now reflect the completed remediation pass.

## Profile

- `initiative`

## Mission

- Add `CanDoItAll.Modules.Processes` as the canonical process-management module inside `CanDoItAll`.
- Keep roles first and concrete executors second so a process remains valid even when the assigned human, supplier, or agent changes.
- Prevent long-term duplication across `CanDoItAll`, `CanDoItAll.AgentFramework`, and future evidence-storage seams such as IPFS.
- Force every implementation phase to stop for architecture, canonical-model, helper-isolation, component-first UI, and large-class review before the next phase may start.

## Key Repairs

- Added the validator-compatible bundle root, phase plan, traceability, inventories, templates, and explicit post-phase repair-bundle generation gates.
- Reopened the shipped implementation after the workbook-backed architecture and user-story audit exposed remaining gaps.
- Executed phase05 remediation for reusable process forms, oversized-file reduction, and realistic software-delivery simulation scenarios.
- Executed phase06 remediation for process-canvas right-click flows, toolbox/create windows, floating selection windows, and definition/runtime action-dialog parity.
- Generated and validated `C:\repositories\CanDoItAll\post-implementation-bundle-phase05` and `C:\repositories\CanDoItAll\post-implementation-bundle-phase06` as explicit phase-gate artifacts.
- Implemented `CanDoItAll.Mcp.Processes` as a simple local stdio MCP over canonical process services, then added install wiring, repo skill sync, restart-ready Codex and VS Code config, and the generated `C:\repositories\CanDoItAll\post-implementation-bundle-phase07` closure artifact.

## Bundle Layout

- `inputs/` raw request, artifacts, and normalized source capture
- `analysis/` repo-fit analysis, assumptions, and risks
- `requirements/` normalized execution and architecture requirements
- `architecture/` target solution, ownership rules, extension points, and phase architecture
- `plan/` phase order, dependency map, critical subbundles, and phase gates
- `traceability/` raw-note and requirement coverage
- `shared-prompts/` reusable prompts for implementation, QA, and post-phase repair generation
- `subbundles/` execution-ready workstreams
- `reviews/` bundle readiness review and execution-report evidence
- `inventories/` feature mapping, cross-repo ownership inventory, and seed-plan details
- `templates/` shared post-phase validation roles, skill pack, and repair-bundle template

## Recommended Execution Order

- The historical and remediation execution order remains in `C:\repositories\CanDoItAll\process-management-bundle\plan\01-phase-plan.md`.
- The final completed remediation sequence was `20 -> 25 -> 21 -> 22 -> 23 -> 24` after the reopen audit.
- The final completed sequence appends `26 -> 27 -> 28`.

## Dependency And Validation Map

- No later phase may start until the prior phase-specific post-implementation repair bundle has been created, validated, and closed or honestly blocked.
- UI-heavy subbundles must use shared CanDoItAll components first, then Playwright proof and screenshot review on large-screen desktop viewports.
- The historical reopen audit remains preserved in:
  - `C:\repositories\CanDoItAll\process-management-bundle\reviews\02-implementation-coverage-audit.md`
  - `C:\repositories\CanDoItAll\process-management-bundle\01-workbooks\01-process-management-execution-grade.xlsx`
  - `C:\repositories\CanDoItAll\process-management-bundle\01-workbooks\02-process-modeling-canvas-and-runtime.xlsx`

## Validation Summary

- Bundle preparation status: `Completed including phase07 MCP access and closure`
- Bundle readiness gate: `Passed`
- Execution status: `Completed`
- Subbundle gate review: `Passed through phase07`
- Final closure gate: `Passed`
- Browser validation analytics: `Phase07 is non-visual; prior UI analytics remain recorded in reviews/01-execution-report.md`

## Execution Highlights

- Implemented the process module shell, persistence, governance model, role-first staffing model, runtime state machine, journals, artifacts, analytics, and project-scoped process projections.
- Extracted reusable process-definition, role, step, role-assignment, and artifact-expectation editor components so the canvas no longer depends on duplicated inline forms.
- Split the largest process-service and workspace logic into clearer partial slices, keeping canvas orchestration and service read/runtime responsibilities separated.
- Expanded the development seed baseline with realistic software-delivery, hotfix rollout, customer onboarding, and incident-response scenarios, including blocked states, approvals, decisions, artifacts, and capability-gap signals.
- Added process-canvas context actions, template-aware create flows, toolbox and selection floating windows, and definition/runtime action dialogs aligned with the shared project-structure workbench vocabulary.
- Added a local process MCP server with typed definition and runtime tools, shared migration bootstrap reuse, focused unit and integration coverage, a dedicated installer, and central reinstall-script registration.
- Installed the MCP into the standard repo workflow, updated `.vscode\mcp.json`, updated `%USERPROFILE%\.codex\config.toml`, synced `candoitall-processes-mcp`, and recorded the new entrypoint in `.artifacts\mcp-installs\install-manifest.json`.
- Validated the full reopened work with targeted integration tests, focused Playwright regression, generated phase05/06/07 repair bundles, install-flow proof, and final bundle validation.
