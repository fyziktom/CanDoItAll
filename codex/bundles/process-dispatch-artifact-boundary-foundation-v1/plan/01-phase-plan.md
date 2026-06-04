# Phase Plan

## Subbundle Dependency Map

```mermaid
flowchart TD
    SB01[SB01 Entry audit] --> SB02[SB02 Artifact inventory]
    SB02 --> SB03[SB03 Seam design]
    SB03 --> SB04[SB04 Refactor Gate A]
    SB04 --> SB05[SB05 Matcher and lineage helpers]
    SB05 --> SB06[SB06 Projection planner foundation]
    SB06 --> SB07[SB07 First projection migration]
    SB07 --> SB08[SB08 Refactor Gate B]
    SB08 --> SB09[SB09 Additional projection adapters]
    SB09 --> SB10[SB10 Validation rule service foundation]
    SB10 --> SB11[SB11 Refactor Gate C]
    SB11 --> SB12[SB12 Final red-team and cutline]
```

## Critical Subbundles

- SB02 is critical because missing a projection/validation branch will cause semantic regression later.
- SB04 is critical because it freezes guardrails before production movement.
- SB07 is critical because it is the first real projection migration.
- SB10 is critical because validation rules are high-risk and must not weaken artifact satisfaction.
- SB12 is critical because it decides whether a next artifact-validation continuation or narrow core-prep bundle is safe.

## Phase Gates

### Gate A After SB04

Required before SB05 starts:

- Source inventory complete.
- No production movement except tests/docs/guardrails.
- Full build or module build plus relevant unit tests.
- No MAF product dependency.
- No Process Core/driver project.
- No prohibited viewport artifacts.

### Gate B After SB08

Required before SB09 starts:

- First execution-artifact projection path migrated through the planner.
- Existing artifact-lineage tests pass.
- External reference key and duplicate suppression tests pass.
- Dispatcher line-count review recorded.

### Gate C After SB11

Required before final closure:

- Validation helper/service covers selected rules.
- Required artifact negative tests pass.
- Process-filtered integration smoke passes or exact blocker recorded with source-level proof.
- No UI/prohibited viewport artifacts.

## Browser Validation Analytics

For all subbundles, default to `N/A - service/runtime refactor only`. If unexpectedly needed, record large-screen PC-only proof.
