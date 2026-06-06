# Codex implementation prompt

You are implementing `process-dispatch-core-readiness-multi-phase-isolation-v1` on branch `maf-processes-refactor`.

Important rules:

1. This is refactoring only. Preserve all behavior.
2. Do not create `CanDoItAll.Processes.Core`.
3. Do not create production driver APIs.
4. Do not touch UI/Razor/CSS/JS/TS or create mobile/small/medium screenshots.
5. Execute SB001..SB024 in order.
6. Stop at SB003, SB006, SB009, SB012, SB015, SB018, SB021, and SB024 until the gate proof is recorded.
7. Do not make wrapper-only moves. Every moved boundary must reduce dispatcher ownership or make a future reduction explicit.
8. Add characterization tests before moving risky behavior.
9. Preserve route order, claim lifecycle, recovery behavior, subprocess behavior, artifact projection, finalizer behavior, and failure closure.
10. The execution report must have one row per subbundle.
