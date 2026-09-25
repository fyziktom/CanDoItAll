# CanDoItAll — MAF 1.22 upgrade and process reliability

**Execution brief for Codex GPT-6 Astra, reasoning effort xhigh**  
Prepared: 2026-09-23. Language of all repository changes, prompts, UI text, tests, and maintained documentation: English.

## What this pack asks you to deliver

Upgrade the current development-based CanDoItAll checkout to Microsoft Agent Framework **1.22.0**, resolve the relevant 1.21 and 1.22 breaking changes, repair the source defects documented here, and demonstrate correct project-agent chat, workflow, and process behavior. Reduce unnecessary human escalation through evidence-based recovery, not by weakening governance.

This is an outcome-driven brief, not a fixed sequence of micro-bundles. Choose the design, editing order, helper abstractions, and commit boundaries. Improve this plan where current source disproves it; preserve the required outcomes and explain material changes. Supporting documents are a risk map and acceptance contract, not instructions to introduce every upstream feature.

## Start

Give Codex **[01_CODEX_PROMPT.md](01_CODEX_PROMPT.md)** and make this entire directory available in its workspace. Read the baseline, migration matrix, findings, and validation plan before editing. The remaining documents provide acceptance journeys, compatibility rules, and reporting forms.

The audit examined `development` at **ffa83cf903c305a7490a674f41a0d08498156db6**. This SHA is an **evidence anchor, not an execution pin**. Start from the user's prepared branch based on current `development`; record the actual starting HEAD and reconcile any drift. Never reset the user's branch to the audit SHA.

The audited source already uses **MAF 1.20.0**, not 1.13 or 1.15, and includes the PostgreSQL 18 merge. The target A2A preview is **1.22.0-preview.260918.1**, verified in the official NuGet listing. Treat the earlier conversational release summaries as superseded by this source-grounded pack.

## The only fixed ordering constraints

1. Use **CanDoItAll CodeAnalytics MCP first** for architecture/test ownership investigation. During editing, use its impacted-test analysis against the actual diff, then build and run the selected tests.
2. Keep the expensive broad/unfiltered suites for the final integration checkpoint. Run the full required Windows and Linux non-browser suites there.
3. Start browser/UI execution only after the required standard non-browser tests are green. Then run the browser suites and the real-provider acceptance journeys. A backend change discovered through UI testing reopens its automated-test gate before another UI attempt.

## Map of the pack

| Document | Purpose |
|---|---|
| [02_BASELINE_AND_ARCHITECTURE.md](02_BASELINE_AND_ARCHITECTURE.md) | Actual dependencies, current layout, boundaries and reviewed source |
| [03_MIGRATION_MATRIX.md](03_MIGRATION_MATRIX.md) | Required versus conditional upstream changes and migration decisions |
| [04_FINDINGS_AND_REPRODUCTIONS.md](04_FINDINGS_AND_REPRODUCTIONS.md) | Concrete source findings and regression reproductions |
| [05_PROCESS_RECOVERY.md](05_PROCESS_RECOVERY.md) | Autonomous recovery, side effects, escalation and finalizer acceptance |
| [06_CODEANALYTICS_AND_TEST_PLAN.md](06_CODEANALYTICS_AND_TEST_PLAN.md) | Impact selection, discovery, platform and full-suite gates |
| [07_UI_ACCEPTANCE_JOURNEYS.md](07_UI_ACCEPTANCE_JOURNEYS.md) | Project chats, approvals, workflows and process UI proof |
| [08_UPGRADE_COMPATIBILITY_AND_ROLLBACK.md](08_UPGRADE_COMPATIBILITY_AND_ROLLBACK.md) | Retained state, old sessions, deployment and safe rollback |
| [09_ACCEPTANCE_CHECKLIST.md](09_ACCEPTANCE_CHECKLIST.md) | Completion criteria |
| [10_DELIVERY_REPORT_TEMPLATE.md](10_DELIVERY_REPORT_TEMPLATE.md) | Final report and evidence expectations |
| [reference/SOURCE_INDEX.md](reference/SOURCE_INDEX.md) | Portable primary-source links, including pinned repository sources |
| [reference/test_map.csv](reference/test_map.csv) | Requirements-to-test/UI traceability; not precomputed MCP selectors |
| [templates](templates/README.md) | Empty execution-result and analysis scaffolds |
| [PACK_REVIEW.md](PACK_REVIEW.md) | Review of this preparation package, not a product test result |

## Evidence boundary

Preparation used GitHub source inspection, official upstream release/PR/code material, and NuGet dependency metadata. Some large files were read in relevant ranges; the manifest distinguishes that from full-file review. **No CanDoItAll build, CodeAnalytics invocation, regression test, Windows/Linux suite, or UI journey was run during preparation.** There is no claim that the reported production escalations have been reproduced or fixed. Codex must establish that evidence.

The optional `python -B tools/validate_pack.py` (Python 3.10 or newer) checks this package's integrity and internal links only. It does not test CanDoItAll. The bundled SHA-256 manifest detects accidental changes; it is not a signed authenticity guarantee. Keep the original reference package immutable; put implementation reports and captured evidence in a separate working output directory.
