# CanDoItAll Microsoft Agent Framework 1.13 Conservative Update Preparation

Generated: 2026-07-07  
Repository: `fyziktom/CanDoItAll`  
Target branch: `memory-providers`  
Primary objective: update Microsoft Agent Framework NuGet packages from the current 1.8-era references to the current 1.13 line, then fix only the compile/runtime regressions caused by that package update.

This bundle is intentionally conservative. It is not a process-runtime redesign, not a new memory architecture, not a tool-surface expansion, and not an adoption pass for new MAF features. The first stage must preserve current behavior as closely as possible.

## What Codex should do

1. Capture the current baseline package and build state.
2. Update the narrow MAF package set.
3. Fix breaking-change compile errors in the existing adapter seams.
4. Run focused regression tests around agent runtime, providers, process dispatch, workflows, approvals, and finalizers.
5. Record evidence and stop.

## What Codex must not do in this stage

- Do not introduce a new `ProcessAgentRuntimeToolProvider`.
- Do not expand `/api/processes` beyond the current route set.
- Do not centralize all package versions unless central package management already exists in the branch.
- Do not refactor `MafAgentRuntime` or process dispatch just because the update exposes architectural smell.
- Do not adopt Foundry hosting, Durable workflows, DevUI, new FileMemory/FileAccess APIs, or new skill-source caching as product features yet.
- Do not hide compile breaks by removing tools, structured-output enforcement, finalizers, approval gates, traces, or process evidence requirements.

## Contents

| Path | Purpose |
| --- | --- |
| `docs/01-current-architecture-map.md` | Source-grounded architecture map and MAF/process/provider boundaries. |
| `docs/02-nuget-update-inventory.md` | Current package references, target versions, and update rules. |
| `docs/03-breaking-change-risk-map.md` | MAF 1.9 to 1.13 breaking-change risk map for CanDoItAll. |
| `docs/04-codex-execution-plan.md` | Step-by-step conservative update plan. |
| `docs/05-validation-and-regression-plan.md` | Required build, test, smoke, and source-scan gates. |
| `docs/06-codex-prompts.md` | Copy-paste prompts for Codex. |
| `docs/07-architecture-decision-record.md` | ADR for the first-stage MAF update. |
| `docs/08-file-touch-plan.md` | Expected touched files and compile-break triage map. |
| `checklists/pre-merge-checklist.md` | Human and Codex pre-merge evidence checklist. |
| `codex/skills/bundles/maf-1.13-conservative-update/` | Skill bundle with phased subbundles/checkpoints. |
| `scripts/Verify-MafUpdate.ps1` | Local verification helper. |
| `scripts/Collect-MafInventory.ps1` | Package inventory helper. |
| `data/package-update-matrix.json` | Machine-readable package matrix. |

## Recommended execution order

Use the skill bundle first:

```powershell
# From repository root after copying the bundle into codex/skills/bundles or using it as a prompt source.
Get-Content .\codex\skills\bundles\maf-1.13-conservative-update\SKILL.md
```

Then execute the plan in order:

1. `00-inventory-and-freeze`
2. `01-package-version-update`
3. `02-compile-break-adapter-fixes`
4. `CHECKPOINT-after-update`
5. `03-focused-regression-validation`
6. `04-documentation-and-merge-evidence`
