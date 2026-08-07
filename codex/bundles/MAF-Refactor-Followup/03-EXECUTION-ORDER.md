# Execution order and checkpoints

## Required path

```text
SB00
  -> SB01 -> SB02 -> SB03 (CP1 authority)
  -> SB04 -> {SB05 + SB06} -> SB07 (CP2 scope/lifetime)
  -> SB08 -> SB09 -> SB10 (CP3 state/continuation)
  -> SB11 -> SB12 -> SB13 (CP4 governance/approval)
  -> SB14
  -> SB16
  -> SB17 (CP5 final merge gate)
```

`SB15` is optional after `SB14` and is not a merge blocker.

## Parallelism

- SB05 and SB06 may run in parallel only after SB04, but they touch shared workspace lifetime code and must merge through SB07.
- No other implementation subbundles should run in parallel unless the checkpoint owner confirms non-overlapping files and invariants.
- Checkpoints are review-only. Do not hide implementation fixes inside a checkpoint.

## Stop conditions

- Authority still comes from UI projection or current navigation.
- A run has more than one workspace scope or process host.
- Approval continuation replays or recaptures instead of restoring exact state.
- MAF regains product/process ownership.
- A test failure is solved by extending an allow-list rather than fixing or explicitly deciding the contract.
- A new project-reference cycle or broad Common project is introduced.
