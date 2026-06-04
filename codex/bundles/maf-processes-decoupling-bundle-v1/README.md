# MAF / Processes Decoupling Bundle v1

Bundle preparation status: `Ready`
Bundle readiness gate: `Ready for Codex execution`
Execution status: `Completed`
Subbundle gate review: `SB01-SB09 passed`
Final closure gate: `Passed`
Browser validation analytics: `N/A for this bundle; no rendered UI route exercised`

## Purpose

This bundle prepares the first safe refactoring phase that decouples `CanDoItAll.AgentFramework.Maf` from `CanDoItAll.Modules.Processes` without changing process runtime semantics, process tool names, access checks, approval behavior, or artifact/recovery behavior.

The immediate target is intentionally narrow:

```text
Before:
CanDoItAll.AgentFramework.Maf
  -> CanDoItAll.Modules.Processes
  -> ProcessesService and process template services

After:
CanDoItAll.AgentFramework.Maf
  -> CanDoItAll.AgentFramework.Tooling
  -> registered IAgentRuntimeToolProvider instances

CanDoItAll.Modules.Processes
  -> CanDoItAll.AgentFramework.Tooling
  -> registers ProcessAgentRuntimeToolProvider
```

The bundle does **not** extract process core yet and does **not** introduce full process driver packs yet. It creates the seam that makes those later phases safe.

## Why This Must Be Done First

Before this bundle, `MafAgentRuntime.ProcessTools.cs` sat inside the MAF adapter and directly imported `CanDoItAll.Modules.Processes`. That made the provider/runtime adapter depend on the product process module. This was the wrong direction for long-term generic process support and made future driver extraction harder.

The current process dispatcher is also very large: 33 partial files and about 25k lines under `src/CanDoItAll.Modules.Processes/Automation/Dispatch`. This bundle avoids touching dispatcher behavior except for proof and smoke coverage. The first cut must be dependency inversion, not a full dispatcher rewrite.

## Non-Negotiable Constraints

- Do not simplify or rename existing process tool names.
- Do not drop any process tool from the MAF-exposed runtime surface.
- Do not weaken `AgentProcessAccessMetadata` / process read/write / allowed definition checks.
- Do not weaken approval wrapping for process mutations.
- Do not move process dispatcher logic during this bundle.
- Do not start the process core split in this bundle.
- Do not introduce DotNet/SWDev/business process driver packs in this bundle.
- Do not make MAF depend on Processes through a different namespace, reflection shortcut, service locator helper, or test-only bypass.
- Do not let tests pass by removing coverage, loosening static assertions, or deleting process tools.


## Validation Summary

Bundle preparation status: `Ready`
Bundle readiness gate: `Ready for Codex execution with repo-root validation`
Execution status: `Completed`
Subbundle gate review: `SB01-SB09 entry and closure passed`
Final closure gate: `Passed`
Browser validation analytics: `N/A for this bundle; no rendered UI route exercised`

- Prepared-stage bundle structure is expected to pass `validate_bundle.py --stage prepared --profile initiative --repo-root <repo-root>`.
- Prepared-stage bundle validation passed before execution on 2026-06-03.
- SB01 completed with artifact-backed baseline proof under `proof/SB01/`.
- SB02 completed with provider-neutral Tooling contracts and artifact-backed proof under `proof/SB02/`.
- SB03 completed with MAF provider composition and artifact-backed proof under `proof/SB03/`.
- SB04 completed with Processes-owned process tool provider migration and artifact-backed proof under `proof/SB04/`.
- SB05 completed with direct MAF -> Processes project reference removal and artifact-backed proof under `proof/SB05/`.
- SB06 completed with parity, policy, provider-registration, and architecture regression proof under `proof/SB06/`.
- SB07 completed with real app-composition runtime provider proof, zero-provider MAF proof, process outbox smoke, tool-receipt semantics, and artifact-lineage smoke under `proof/SB07/`.
- SB08 completed with provider-seam documentation, operator troubleshooting, stale-reference scans, and documentation source assertions under `proof/SB08/`.
- SB09 completed with final hidden-dependency scans, parity/policy/runtime smoke reruns, red-team review, next-phase readiness, proof audit, and final closure under `proof/SB09/`.
- Critical foundations require artifact-backed proof manifests and semantic invariants.
- Runtime smoke proof completed in SB07.
- Browser validation was not required because no subbundle exercised or changed a rendered UI route.

## Bundle Contents

- `inputs/` raw request and source-grounded findings.
- `analysis/` current-state and risk analysis.
- `requirements/` normalized requirements and hard constraints.
- `architecture/` target dependency shape and code movement plan.
- `inventories/` tool inventory, source references, test-impact inventory.
- `plan/` dependency-aware subbundle sequence.
- `subbundles/` nine implementation-ready subbundles.
- `evidence/checklists/MAF_Processes_Decoupling_Checklists.xlsx` detailed execution checklist workbook.
- `traceability/` requirement-to-subbundle mapping.
- `shared-prompts/` reusable Codex implementation and QA prompts.
- `reviews/` self-review and seeded execution report.

## Execution Order

1. SB01 baseline inventory and proof plan.
2. SB02 add agent runtime tooling abstraction project.
3. SB03 teach MAF to consume registered runtime tool providers while keeping current process tool path temporarily.
4. SB04 migrate process tool builder into the Processes module as a provider.
5. SB05 remove MAF -> Processes reference and old process tool partial.
6. SB06 repair/add parity and policy regression suite.
7. SB07 composition registration and runtime smoke.
8. SB08 docs/operator handoff.
9. SB09 final red-team closure and next-phase readiness.

## Stop Conditions

Stop and reopen the relevant subbundle if any of these occur:

- `CanDoItAll.AgentFramework.Maf.csproj` still references `CanDoItAll.Modules.Processes`.
- `src/CanDoItAll.AgentFramework.Maf` still contains `using CanDoItAll.Modules.Processes`.
- `MafAgentRuntime.Capabilities.cs` still contains `ProcessToolBuilder` or `CreateProcessToolBuilder`.
- Any process tool listed in `inventories/01-process-tool-parity-inventory.md` is missing after migration.
- Read tools are wrapped in approvals or mutation tools are no longer approval-wrapped.
- MAF cannot be built/instantiated without the Processes module registered.
- Existing process automation smoke or seeded process tests lose tool receipts, current-run artifact lineage, or governed finalizer behavior.
