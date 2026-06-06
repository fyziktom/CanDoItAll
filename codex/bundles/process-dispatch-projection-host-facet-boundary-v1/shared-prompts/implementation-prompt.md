# Suggested implementation prompt

You are implementing `process-dispatch-projection-host-facet-boundary-v1` on branch `maf-processes-refactor`.

Execute SB01 through SB72 in order. Do not skip critical gates. This is a behavior-preserving refactor only.

Hard constraints:

- Do not create Process Core.
- Do not create production process-driver APIs.
- Do not touch UI/Razor/CSS/JS/TS.
- Do not produce small/medium/mobile proof artifacts.
- Preserve projection source-family order and all existing functionality.
- If a gate fails, reopen and repair the last source-moving subbundle.
