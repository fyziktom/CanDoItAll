# Phase Plan

## Phase Sequence

1. Prepare and validate the shared provider model selector contract.
2. Implement the shared component and focused component tests.
3. Integrate the component into the Agents Runtime tab, normalize save semantics, and update impacted tests.
4. Review workflow and memory surfaces, adopting the component where the existing contract supports it.
5. Run targeted tests, capture browser proof or an explicit blocker, close raw input, and run final validators.
6. Reopen the agent Runtime tab save/reload semantics so explicit override is canonical and cannot be collapsed to provider-default linkage unless the saved model is empty.

## Subbundle Dependency Map

```mermaid
gantt
title Provider Default Model Picker Dependency Map
dateFormat  YYYY-MM-DD
section Foundation
Shared selector contract and component :crit, s1, 2026-05-20, 1d
section Integration
Agents runtime and dependent surfaces :s2, after s1, 1d
section Follow-up Repair
Explicit override canonicity :crit, s3, after s2, 1d
section Closure
Tests, browser proof, and raw-note closure :milestone, after s3, 0d
```

## Critical Subbundles

- `01-shared-provider-model-choice-foundation` is critical because all later UI surfaces depend on its default, suggested-model, and override semantics.
- `02-agents-runtime-tab-and-dependent-surfaces` depends on subbundle 01 and must not ship if the selector cannot preserve provider-default linkage.
- `SB03-explicit-model-override-canonicity` is critical because it repairs the source-of-truth rule exposed by the follow-up: empty model means linked provider default; any non-empty model string means explicit override.

## Phase Gates

- Gate after preparation: run `validate_bundle.py --stage prepared --profile initiative` and manual readiness audit.
- Gate before subbundle 01: confirm provider model fields and BaseLib components are still present.
- Gate after subbundle 01: component tests prove default, suggested, and override behavior.
- Gate before subbundle 02: confirm subbundle 01 is complete and selector API is stable.
- Gate after subbundle 02: targeted tests and browser proof show agent Runtime tab behavior and no dependent surface regression.
- Gate before subbundle 03: confirm prior selector/provider-default behavior still has passing tests and the follow-up source references still exist.
- Gate after subbundle 03: failing-first and passing tests prove explicit override survives save/reload while provider-default linkage still saves empty.
- Final gate: close raw request and follow-up as Solved or document concrete follow-up if browser proof cannot be captured.
