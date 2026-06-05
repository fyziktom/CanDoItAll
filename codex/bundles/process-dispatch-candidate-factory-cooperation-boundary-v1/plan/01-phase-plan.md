# Phase Plan

## Execution Order

- Execute subbundles strictly in SB01 through SB18 order.
- Stop at each critical gate before allowing dependent production movement.
- Reopen the earliest affected subbundle if later proof weakens a prerequisite.

## Subbundle Dependency Map

```mermaid
flowchart TD
  SB01[SB01 Entry audit] --> SB02[SB02 Candidate field map]
  SB02 --> SB03[SB03 Seam design]
  SB03 --> SB04[SB04 Gate A]
  SB04 --> SB05[SB05 Assembly context]
  SB05 --> SB06[SB06 Subprocess factory]
  SB06 --> SB07[SB07 Workflow factory]
  SB07 --> SB08[SB08 Gate B]
  SB08 --> SB09[SB09 Direct-agent factory]
  SB09 --> SB10[SB10 Binding integration cleanup]
  SB10 --> SB11[SB11 Recovery intent assembly]
  SB11 --> SB12[SB12 Gate C]
  SB12 --> SB13[SB13 Cooperation resolver]
  SB13 --> SB14[SB14 Driver-readiness map]
  SB14 --> SB15[SB15 Slimming pass]
  SB15 --> SB16[SB16 Gate D]
  SB16 --> SB17[SB17 Final red-team]
  SB17 --> SB18[SB18 Next cutline]
```

## Critical Subbundles

- SB04: first hard architecture gate before production movement.
- SB08: subprocess/workflow candidate parity gate.
- SB12: direct-agent candidate parity and side-effect gate.
- SB16: runtime smoke and line-count gate.
- SB17: final red-team closure.

## Phase Gates

| Gate | Subbundle | Must prove |
| --- | --- | --- |
| Gate A | SB04 | Architecture guardrails; no core/driver/UI/prohibited proof; factory side-effect bans. |
| Gate B | SB08 | Subprocess and workflow candidate parity. |
| Gate C | SB12 | Direct-agent, binding, recovery parity. |
| Gate D | SB16 | Build, focused tests, source scans, line-count review. |
| Final | SB17 | Completed proof pack and raw-note closure. |
