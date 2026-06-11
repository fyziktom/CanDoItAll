# Target solution

- Keep Process Core generic and dependency-clean.
- Use process-owned launch, outbox, dispatch, finalizer, and readback paths for representative proof.
- Keep manual-transition tests as persistence or state contract tests, not representative automation E2E proof.
- Surface runtime-host diagnostics through run detail UI when feasible; otherwise provide explicit API proof and tracked UI follow-up.
- Close only when source code, tests, Playwright proof, source scans, and the code-first ratio agree.

