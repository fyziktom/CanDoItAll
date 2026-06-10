# Phase Plan

## Subbundle Dependency Map

```mermaid
graph TD
  SB01[SB01 Code-first ratio + baseline guard]
  SB02[SB02 Runtime-host contracts]
  SB03[SB03 Dry-run host pipeline]
  SB04[SB04 Durable audit + retention-ready readback]
  SB05[SB05 Capability provider/catalog boundary]
  SB06[SB06 Scheduler/workflow read-only job lifecycle]
  SB07[SB07 Manager/operator runtime-host readback]
  SB08[SB08 Release matrix + final red-team]
  SB01 --> SB02 --> SB03 --> SB04 --> SB05 --> SB06 --> SB07 --> SB08
```

## Critical Subbundles
All subbundles are critical. There are only 8 because each must own a coherent implementation area, not a micro-change.

## Phase Gates
Each subbundle must record:

- changed source/test files,
- focused test command and result,
- anti-stub scan,
- boundary scan,
- code-first ratio impact,
- explicit downstream impact.

## Final Gate
Completion requires:

- build pass,
- full unit pass,
- focused integration pass,
- live process-run smoke classification,
- code-first ratio pass,
- no Core dependency drift,
- no reflection discovery/self-registration/fallback selector,
- no execution-capable effects.
