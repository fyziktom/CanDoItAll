# MAF / Processes Decoupling Bundle v1

Bundle preparation status: `Ready`
Bundle readiness gate: `Ready for Codex execution`
Execution status: `Not started`
Subbundle gate review: `Not started`
Final closure gate: `Not started`
Browser validation analytics: `Planned; required only for runtime/UI smoke phases`

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

`MafAgentRuntime.ProcessTools.cs` currently sits inside the MAF adapter and directly imports `CanDoItAll.Modules.Processes`. That makes the provider/runtime adapter depend on the product process module. This is the wrong direction for long-term generic process support and makes future driver extraction harder.

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
Execution status: `Not started`
Subbundle gate review: `Not started`
Final closure gate: `Not started`
Browser validation analytics: `Planned; required only for SB07 if runtime/UI surfaces are exercised`

- Prepared-stage bundle structure is expected to pass `validate_bundle.py --stage prepared --profile initiative --repo-root <repo-root>`.
- Critical foundations require artifact-backed proof manifests and semantic invariants.
- Runtime proof is deferred to SB07 because this preparation bundle does not implement code.
- Browser validation is planned only for SB07 if runtime/UI smoke touches rendered surfaces.

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
