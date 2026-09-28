# Plugins UI decoupling, with TestLab closure first

**Execution entry:** [prompt.md](prompt.md).

This is one implementation assignment for Codex GPT-6 Astra Max: confirm the preceding TestLab corrections, finish any small reproducible carry-over, and then implement the **complete existing Plugins workspace** with a real backend-free sandbox. Do not stop after TestLab or after producing a plan.

Review provenance: `fyziktom/CanDoItAll`, `components-decoupling`, `dd050d5a1489537207e073cac0838f40cde4340f`, reviewed on 2026-09-28. This is not an execution checkout pin. Use the actual authorized checkout and reconcile drift.

## Read with the prompt

- [TestLab closure review](TESTLAB_CLOSURE_REVIEW.md): R1/R2/C1 are addressed in source; current implementer receipts are not independently rerun tests.
- [Plugins source review and target architecture](PLUGINS_REVIEW_NOTES.md): existing behavior, concrete defects and bounded ownership decisions.
- [Validation matrix](VALIDATION_MATRIX.md): meaningful proof, safe production fixtures and exact evidence requirements.
- [Development-loop proof](DEV_LOOP.md): actual graph, assets and measured edit-to-visible behavior.
- [Sources](SOURCES.md) and [machine-readable register](sources.json): pinned files and explicit partial-read limits.
- [Shared v3](shared/README.md): the 22-file foundation is included unchanged. Its audit/module map is historical, not the present module-status authority.

The current repository instructions, `docs/architecture/ui-component-seams.md`, `docs/testing.md` and current CI remain authoritative. The module-specific notes refine the scope; they do not replace the product's shared rules.

## Current decision

Proceed to Plugins after S0. No new blocking TestLab defect was identified in this source review. There is no requirement to invent another TestLab change. Plugins contains six existing detail sections (Main info, Executors, Settings, Connections, Logs, Grants), a catalog tree, plugin lifecycle actions and a package installation/upload dialog with restart state. Extract the whole current surface, not a read-only catalog demonstration.

Suggested projects are `CanDoItAll.Modules.Plugins.Contracts`, `CanDoItAll.Plugins.UI` and `CanDoItAll.Plugins.UiSandbox`. Equivalent existing seams take precedence. Keep production services in process; no API-only rewrite, database migration or plugin-runtime redesign is authorized.

The package is a text handoff, not a product patch. [Package validation](PACKAGE_VALIDATION.md) is separate from product test results. Previous module-specific bundles are not recursively copied again; the current closure review and unchanged shared foundation provide the relevant context without accumulated obsolete execution instructions.
