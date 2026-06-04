# MAF / Runtime Tool Provider Hardening Follow-up v1

Bundle preparation status: `Prepared`
Bundle profile: `initiative`
Target branch reviewed: `maf-processes-refactor`
Baseline branch: `development`
Created: `2026-06-04`

## Purpose

This follow-up bundle continues after the completed `maf-processes-decoupling-bundle-v1` work. The previous branch achieved the scoped objective of removing the direct `CanDoItAll.AgentFramework.Maf -> CanDoItAll.Modules.Processes` process-tool dependency by introducing `CanDoItAll.AgentFramework.Tooling` and moving process tools into `ProcessAgentRuntimeToolProvider`.

This bundle **does not start process-core extraction**. It prepares smaller hardening steps before that larger split:

1. Clean branch hygiene and entry proof.
2. Harden the generic runtime tool-provider seam.
3. Providerize remaining hard-coded first-party product tool attachments still living in MAF where feasible.
4. Refactor the Processes-owned tool provider so it does not become a new monolith.
5. Add purpose/security/observability groundwork needed before later manager-verification and driver-pack work.
6. Close with integration smoke, docs, and red-team merge readiness.

## Why This Comes Before Process Core

The branch now has the first dependency-inversion seam, but MAF still has hard-coded product-tool attachment paths for project-structure and image-generation tools, and `ProcessAgentRuntimeToolProvider` is a large provider file. Starting `CanDoItAll.Processes.Core` extraction now would mix too many reasons to change: provider seam hardening, remaining MAF product coupling, process tool provider maintainability, and core/domain split.

This bundle stabilizes those seams first so the later process contracts/core extraction can be smaller and safer.

## Reviewed Findings Summary

| Finding | Status | Follow-up treatment |
| --- | --- | --- |
| Direct `CanDoItAll.AgentFramework.Maf -> CanDoItAll.Modules.Processes` project reference | Closed by previous branch | Recheck in SB01/SB12 hidden dependency scans. |
| New neutral `CanDoItAll.AgentFramework.Tooling` project | Good foundation | Harden descriptor/metadata in SB02. |
| MAF composes registered runtime tool providers | Good foundation | Refactor provider composition and naming in SB03. |
| Processes registers `ProcessAgentRuntimeToolProvider` | Good foundation | Split and purpose-harden in SB07/SB08. |
| MAF still has hard-coded project-structure and image-generation tool attachment | Remaining coupling | Providerize in SB04/SB05. |
| MAF still references Projects/Security/Workbench/Workspace | Remaining architecture debt | Inventory and allowed-list in SB04/SB06/SB12; remove only where no longer needed. |
| Branch diff contains substantial `codex/bundles` churn | Merge-risk / hygiene issue | Classify and clean in SB01 before runtime work. |

## Subbundles

- `SB01` `01-branch-hygiene-entry-proof-and-merge-scope` — Branch hygiene, entry proof, and merge-scope cleanup
- `SB02` `02-runtime-tool-provider-descriptors-and-metadata` — Runtime tool-provider descriptors and metadata contract
- `SB03` `03-maf-provider-composition-policy-refactor-gate` — MAF provider composition policy refactor gate
- `SB04` `04-project-structure-tool-provider-extraction` — Project-structure tool provider extraction from MAF
- `SB05` `05-image-generation-tool-provider-extraction` — Image-generation tool provider extraction from MAF
- `SB06` `06-refactor-checkpoint-provider-boundary-cleanup` — Forced refactor checkpoint: provider boundary cleanup
- `SB07` `07-process-agent-tool-provider-internal-split` — ProcessAgentRuntimeToolProvider internal split
- `SB08` `08-process-provider-purpose-access-and-manager-readonly-hardening` — Process provider purpose/access hardening and manager read-only groundwork
- `SB09` `09-runtime-tool-provider-observability-and-receipt-tagging` — Runtime tool-provider observability and receipt tagging
- `SB10` `10-documentation-and-architecture-guards-refresh` — Documentation and architecture guard refresh
- `SB11` `11-integration-smoke-and-real-process-regression` — Integration smoke and real process regression
- `SB12` `12-final-red-team-merge-readiness-and-next-phase-cutline` — Final red-team, merge readiness, and next-phase cutline

## Execution Rule

Do not collapse phases. After SB03, SB06, and SB09, stop for a refactor checkpoint before continuing. The implementation agent must preserve exact tool parity and policy behavior unless a subbundle explicitly owns a tool-surface change.
