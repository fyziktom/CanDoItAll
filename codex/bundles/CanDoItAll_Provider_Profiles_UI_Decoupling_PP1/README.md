# Provider Profiles Core PP1 · UI decoupling

Continue the existing module-by-module rendering wave. A2 completed the ten-section technical-agent editor. PP1 extracts the complete provider catalog/shell and its four local configuration sections, retaining the actual Sharing, request History and Shared provider connections hosts.

Start with [prompt.md](prompt.md). The [A2 review](A2_REVIEW.md) does **not** identify a new blocking product defect requiring another standalone closure. [Entry closure](S0_ENTRY_CLOSURE.md) verifies current inputs and preserves the proven repairs, then work proceeds into PP1. Do not invent changes merely to fill S0.

The new scope, behavior and proof are described in [architecture](SCOPE_AND_ARCHITECTURE.md), [current source observations](PROVIDER_SOURCE_REVIEW.md), [state and operations](STATE_AND_OPERATIONS.md), [Pricing/Thinking](PRICING_AND_THINKING.md), [retained integrations](RETAINED_INTEGRATIONS.md), [dependencies](DEPENDENCY_AND_DELIVERY.md), [validation](VALIDATION_MATRIX.md) and [application journeys](APPLICATION_JOURNEYS.md). [Execution](EXECUTION_AND_CLOSURE.md) and [desktop/development loop](DESKTOP_AND_DEV_LOOP.md) bound cost and behavior. [Roadmap](ROADMAP.md) distinguishes PP1 from remaining AgentFramework work.

The [shared foundation](shared/README.md) is the original 22-file shared v3, preserved byte-for-byte. Current repository instructions and `docs/architecture/ui-component-seams.md` remain authoritative. This is an implementation brief, not a prescribed new architecture framework.

## Evidence and history

Reviewed application: `25ea60327ab572daee2728d6e86589f035942d94`, `components-decoupling`, 2026-10-02. The current product commit follows entry/archive commit `bf6d15d3b6323a1fb187874afdeb78eb3a55170e`. The SHA is evidence provenance, **not a checkout pin**. Work from the owner-supplied current checkout and refresh drift.

Historical bundles remain in the repository until the owner's separate pre-merge cleanup. Do not delete, modify, re-execute or convert their sealed NOT_RUN templates into this run's status. Write current evidence in a separate task-owned location and maintain the relevant architecture record.

The reviewer read connected source, tests and maintained execution reports, but did not run .NET, PostgreSQL, browser tests or watch measurements. [Evidence assessment](TEST_EVIDENCE_REVIEW.md) distinguishes Codex's prior results from current product proof. This archive contains no application patch or previous private test artifacts.

```text
python -B tools/validate_package.py
python -B tools/test_package.py
python -B shared/tools/validate_bundle.py
python -B shared/tools/test_tooling.py
```

These commands verify this handoff, not the product. [Package validation](PACKAGE_VALIDATION.md) records the actual checks. [Česká revize](REVIEW.cs.md).
