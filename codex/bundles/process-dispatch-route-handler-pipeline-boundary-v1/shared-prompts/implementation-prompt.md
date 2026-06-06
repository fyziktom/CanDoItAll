# Implementation Prompt

You are implementing `process-dispatch-route-handler-pipeline-boundary-v1`.

- Work only on `maf-processes-refactor`.
- Preserve all behavior; this is a refactor and hardening bundle only.
- Do not create Process Core, production driver APIs, UI changes, or browser/mobile proof artifacts.
- Execute `SB001` through `SB112` in numeric order.
- Stop at every critical gate and create manifest, semantic invariants, source scan, test transcript, raw-note closure proof, and anti-stub scan.
- Keep route order exactly aligned with `ProcessDispatchRoutePipeline.StageOrder`.
- Keep side effects visible through named handlers, coordinators, stores, transition handlers, execution handlers, or finalizer handlers.
- Update `bundle://reviews/01-execution-report.md` with one row per subbundle.
