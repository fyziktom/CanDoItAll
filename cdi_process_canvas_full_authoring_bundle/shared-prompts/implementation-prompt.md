# Implementation Prompt

Implement only the current subbundle from `C:\repositories\CanDoItAll\cdi_process_canvas_full_authoring_bundle`.

Rules:

- Read `plan/01-phase-plan.md` and the current subbundle README before changing code.
- Treat `subbundles/01-node-inventory-and-port-semantics`, `subbundles/02-canonical-port-model-and-persistence-foundation`, and `subbundles/03-shared-step-node-multi-port-rendering-and-gesture-parity` as critical foundations.
- Keep CanvasLib generic and keep process semantics in `CanDoItAll.Modules.Processes`.
- Prefer a strongly-typed process-canvas port catalog over scattered string comparisons.
- Do not fake relationships in UI state if the service layer and database cannot persist them.
- Use the minimum coherent code change that closes the current subbundle.
- Add or update focused tests before claiming the subbundle is complete.
- If the subbundle touches browser-visible behavior, run Playwright on `/processes`, capture screenshots, and update `reviews/01-execution-report.md` immediately while evidence is fresh.
- If later work exposes weak proof or a missing earlier assumption, reopen the earlier subbundle instead of patching around it silently.
