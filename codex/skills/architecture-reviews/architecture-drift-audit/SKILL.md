---
name: architecture-drift-audit
description: Recurring architecture drift audit for a C# / Blazor solution. Use explicitly for periodic health checks, before releases, or after several feature waves to detect overloaded abstractions, projection leakage, layering erosion, source-of-truth duplication, and other signs that the architecture is becoming harder to stabilize.
---

# Purpose

Use this skill as a **periodic health check**.

This is lighter than `canonical-model-review`, but still evidence-driven.

The goal is to answer:

1. Where is architecture drift accumulating?
2. Which hotspots are worth stabilizing now?
3. Which drift is acceptable for the moment?
4. Is the next feature wave likely to amplify existing fragility?

# Workflow

## 1. Establish baseline

If the repository contains prior reports under `architecture/reviews/`, read the latest relevant ones first.

If no prior review exists, use the current codebase as the baseline.

## 2. Inventory the current architecture

You may use:

- CanDoItAll CodeAnalytics MCP
- `python codex/skills/architecture-reviews/architecture-drift-audit/scripts/solution_inventory.py --root . --output architecture/reviews/_inventory.json`

Use SharpTools only as a backup if CodeAnalytics has a real unresolved capability gap. Do not switch to SharpTools merely because CodeAnalytics transport needs a restart or reinstall.

Look for:

- project reference growth
- namespace sprawl
- new mixed-role classes
- rising number of catch-all managers/helpers
- domain types shaped by integration concerns
- more places writing the same conceptual truth
- more projections used as write models
- runtime/tooling concerns creeping into core code

## 3. Search for drift signals

Specifically inspect for:

- `*Manager`, `*Helper`, `*Util`, `*Facade`, `*Coordinator` overgrowth
- mapping / serialization attributes inside domain core
- UI components or pages reaching into repositories or infrastructure directly
- DTOs or view models reused as canonical write models
- multiple date/time fields representing the same concept in different layers
- duplicated enum concepts with slightly different names
- token / rights / auth concerns smeared across unrelated models
- snapshot / import / export types treated as live truth
- growing bidirectional dependencies
- TODO / HACK / FIXME around model integrity

## 4. Validate with minimal commands

Run the smallest safe build/test checks that confirm whether the drift is already causing breakage.

## 5. Produce the audit report

Use the template in:

- `codex/skills/architecture-reviews/architecture-drift-audit/assets/review-report-template.md`

Always include:

- overall drift summary
- strongest hotspots
- drift categories
- whether drift is accelerating or stable
- top stabilization actions
- defer/accept decisions where appropriate

# Drift categories

Use these categories:

- Source-of-truth drift
- Boundary drift
- Projection drift
- Policy / auth drift
- Runtime / operational drift
- Integration drift
- Naming / concept drift
- Testability drift
- Dependency drift

# Priority lens

A drift issue is higher priority when it is:

- close to canonical truth
- likely to be touched in the next wave
- hard to test safely
- likely to create parallel truths
- likely to blur permissions / policies
- likely to amplify many-to-many relation confusion

# References

Read these before synthesis if needed:

- `references/drift-signals.md`
- `references/drift-audit-checklist.md`
- `references/priority-lens.md`
