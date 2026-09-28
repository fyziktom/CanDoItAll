# CanDoItAll · Shared UI Decoupling Bundle

**Reference:** `CDA-UI-DECOUPLING-SHARED-v3` · **Edition:** 2026-09-28

Incrementally separate production renderers from persistence, runtime and application composition so a developer can work on a module in a small, representative `dotnet watch` host. Keep the modular application and its existing in-process services. This is **not** an API-only migration, a new UI framework, a backend rewrite, or authorization to refactor every module at once.

## Start here

Read [the execution brief](prompt.md), [boundaries](architecture/01-boundaries.md), [state and effects](architecture/02-state-and-effects.md), [sandboxes and the development loop](architecture/03-sandboxes-and-dev-loop.md), and [validation](VALIDATION.md). Consult [domain safeguards](architecture/04-domain-safeguards.md) where a slice crosses an owner boundary. A module-specific assignment can be a single document based on [the child template](templates/module-slice.md); it does not need numbered subbundles or a prescribed class hierarchy.

The [review](audit/REVIEW.md), [input reconciliation](audit/input-reconciliation.md), [module map](audit/module-map.md) and [source register](audit/source-register.md) explain why this edition differs from the supplied bundles. They are reference material, not a mandatory reading loop for every edit. [České shrnutí](README.cs.md).

## Authority and currency

The current repository's `AGENTS.md`, `.github/copilot-instructions.md`, **`docs/architecture/ui-component-seams.md`**, `docs/testing.md` and applicable CI configuration remain the product authority. Family-wide standards and reusable agent skills belong in `CanDoItAll.SharedInfo`. This bundle is their UI-decoupling execution companion, not a competing permanent architecture manual.

The review used `fyziktom/CanDoItAll`, **development**, commit `7db3543ab437376baeca55089cb331fbe1b30483` (2026-09-27). The branch was checked again at the end of source inspection and had not changed. This identifies the evidence, **not a required execution checkout**. Begin a child on the branch supplied by the owner, record its actual HEAD and dirty state, then refresh the affected source and test map. Do not reset to the review SHA, silently change branches, or assume a newer checkout is compatible because its filenames match.

When code and guidance disagree, distinguish intended behavior, current implementation and a defect. Preserve safety and the authorized behavior; record and resolve the relevant discrepancy at its real owner. Neither historical prose nor accidental current behavior automatically wins. Any lasting clarification belongs in the existing canonical document, not a second copied manual.

## What “done” means for a slice

Production uses the extracted renderers; their complete useful scenario can run without production module implementations or a database; dependencies and assets really reflect that separation; state, mutations and owner authority remain correct; the affected behavior has current evidence. Report architecture, sandbox fidelity, production behavior and measured development performance separately. A working sandbox alone does not close production work, and a smaller graph alone does not prove a speedup.

A bounded preparation step may finish with an explicit remaining extraction blocker. It must not be labelled a completed module. The program tracks surfaces and owner roles, not the number of `.UI` projects.

## Package and tooling

This archive contains guidance and two optional Python 3.10+ utilities. It contains no application patch, dependency binaries, old browser/TRX evidence or fonts. `tools/check_review_drift.py` reads local Git metadata and reports reviewed files that changed; it does not fetch, switch branches, stage files or run tests. `tools/validate_bundle.py` checks this archive's internal integrity, not application correctness.

```text
python tools/validate_bundle.py
python tools/check_review_drift.py --repo <path-to-CanDoItAll>
python tools/test_tooling.py
```

The drift utility prints JSON to standard output (redirect it to an owned evidence file as needed). Exit code 0 means a report was produced, even when drift is found; code 2 means the utility could not complete. It compares registered file blobs and reports working-tree/index changes, not the complete new module inventory.

The review is source/configuration analysis. No .NET build, product test, live browser journey, sibling build or watch benchmark was executed for this package. Prior repository completion reports remain historical evidence with their own scope. See [validation status](audit/package-validation.md).
