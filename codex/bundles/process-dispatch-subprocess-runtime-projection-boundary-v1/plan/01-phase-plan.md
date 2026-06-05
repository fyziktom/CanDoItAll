# Phase Plan

## Execution Order

Execute SB01 through SB24 in numeric order. Do not start a subbundle until the previous subbundle closure gate has passed or has been explicitly reopened and repaired.

## Subbundle Dependency Map

```mermaid
graph TD
  SB01 --> SB02 --> SB03 --> SB04
  SB04 --> SB05 --> SB06 --> SB07 --> SB08
  SB08 --> SB09 --> SB10 --> SB11 --> SB12
  SB12 --> SB13 --> SB14 --> SB15 --> SB16
  SB16 --> SB17 --> SB18 --> SB19
  SB19 --> SB20 --> SB21 --> SB22 --> SB23 --> SB24
```

## Critical Subbundles

- SB04: Architecture guardrails before movement.
- SB08: Lifecycle parity gate before projection movement.
- SB16: Artifact projection parity gate.
- SB19: Dispatch facade parity gate.
- SB23: Line-count and boundary review.
- SB24: Final red-team and next cutline.

## Phase Gates

- Each gate must prove build, focused tests, source scans, and the required proof artifacts before downstream work may continue.
- Critical gates SB04, SB08, SB16, SB19, SB23, and SB24 must include semantic adequacy proof and red-team or verifier evidence where required.
- A failed gate reopens the last production movement subbundle and blocks dependent subbundles until repaired.
