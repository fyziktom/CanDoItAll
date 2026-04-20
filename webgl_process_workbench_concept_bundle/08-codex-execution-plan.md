# Codex execution plan

The authoritative dependency map lives in `plan/01-phase-plan.md`. This root plan summarizes the intended run:

1. Lock the baseline and the renderer decision.
2. Add the universal WebGL library.
3. Build the runtime foundation.
4. Run Gate A.
5. Add the process-template adapter.
6. Add the dedicated sandbox project.
7. Add authoring interactions in sandbox-only state.
8. Run Gate B.
9. Add automation bridge and semantic proof.
10. Close with final validation and migration guidance.

At every gate, weak proof blocks downstream work.
