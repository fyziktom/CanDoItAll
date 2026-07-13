# CanDoItAll MAF 1.13 Conservative Update Execution Bundle

Generated: 2026-07-07
Repository: `C:\repositories\CanDoItAll`
Current branch observed during preparation: `memory-providers`
Profile: `initiative`

## Mission

Prepare and execute a conservative Microsoft Agent Framework package update from the current 1.8-era references to the 1.13 line, fixing only package-induced compile/runtime regressions while preserving current CanDoItAll process, provider, workflow, approval, finalizer, telemetry, context, and evidence behavior.

## Outcome Contract

- Requested outcome: implementation-ready bundle for the MAF 1.13 package update.
- Hard constraints: no implementation during preparation; no direct process runtime tool provider; no process API expansion; no broad runtime refactor; no new MAF feature adoption in phase 1.
- Evidence required before closure: restore/build/test transcripts, package before/after table, preview-package decisions, source scans, architecture drift review, changed-file hashes for critical subbundles, and final evidence note.
- Known blockers or explicit scope exceptions: Mem0 preview package was not found from configured NuGet sources during preparation; Playwright/service smoke may be environment-dependent and must be recorded honestly.

## Bundle Layout

- `inputs/` raw request, source prep bundle copy, and structured input
- `analysis/` current state, assumptions, risks, package evidence, and reopen triggers
- `requirements/` normalized requirements
- `architecture/` C# current-state inventory, boundaries, dependency direction, pattern selection, and testability plan
- `plan/` execution order, dependency map, and architecture checkpoints
- `traceability/` requirement-to-subbundle mapping
- `shared-prompts/` reusable implementation and QA prompts
- `subbundles/` numbered execution-ready workstreams
- `reviews/` self-review, execution report, and C# architecture gate
- `checklists/` detailed workbook checklist

## Recommended Execution Order

1. `subbundles/01-sb01-inventory-and-freeze`
2. `subbundles/02-sb02-package-version-update`
3. `subbundles/03-sb03-compile-break-adapter-compatibility`
4. `subbundles/04-sb04-architecture-drift-checkpoint`
5. `subbundles/05-sb05-focused-regression-validation`
6. `subbundles/06-sb06-evidence-and-merge-readiness`

## Dependency And Validation Map

Use `plan/01-phase-plan.md` as the durable phase map. Critical subbundles are `SB01`, `SB02`, `SB03`, and `SB04`; downstream validation is not trustworthy until they pass.

## Source Inputs

- `bundle://inputs/original-prep/README.md`
- `bundle://inputs/original-prep/docs/01-current-architecture-map.md`
- `bundle://inputs/original-prep/docs/02-nuget-update-inventory.md`
- `bundle://inputs/original-prep/docs/03-breaking-change-risk-map.md`
- `bundle://inputs/original-prep/docs/04-codex-execution-plan.md`
- `bundle://inputs/original-prep/docs/05-validation-and-regression-plan.md`
- `bundle://inputs/original-prep/docs/07-architecture-decision-record.md`
- `bundle://inputs/original-prep/data/package-update-matrix.json`
- `bundle://checklists/maf-1.13-phase-checklists.xlsx`

## Non-Negotiable Constraints

- Do not introduce `ProcessAgentRuntimeToolProvider`.
- Do not add direct `processes_*` runtime tools in this package update.
- Do not expand `/api/processes` routes.
- Do not move process-domain behavior into MAF infrastructure.
- Do not introduce central package management unless an existing central package file is found before implementation starts.
- Do not broadly suppress warnings to hide package/API drift.
- Do not remove or weaken approvals, required finalizers, structured-output contracts, runtime tool ownership traces, context manifests, provider lane gates, serialized-session compatibility, or process evidence.
- Do not adopt new MAF 1.13 features such as Foundry hosting, Durable workflows, DevUI, FileMemory/FileAccess feature surfaces, or new skill-source caching as product features in this phase.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Prepared-stage validator passed after workbook generation`
- Execution status: `Implemented`
- Subbundle gate review: `SB01 through SB06 completed`
- Final closure gate: `Passed with noted local test-host rerun limitation`
- Browser validation analytics: `SB05 live 5032 project-structure floating-chat PDF-to-XLSX validation passed`
