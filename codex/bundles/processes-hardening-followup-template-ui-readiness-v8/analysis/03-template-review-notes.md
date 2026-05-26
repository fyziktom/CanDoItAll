# Template Review Notes

## Blazor WASM PWA / Tetris readiness

The future Tetris scenario should use the Blazor app delivery template, but the template should be tightened first:

- The first step must only resolve the delivery contract, not implement.
- Implementation must create the app and produce durable implementation change-set evidence.
- Validation must be read-only against the product target and must capture build, startup, browser, screenshot, and console proof.
- QA/revalidation must not mutate product files.
- Repair steps must be the only steps that mutate product files after validation detects issues.
- Final result/writeback steps should not mutate product source files; they should write managed artifacts and controlled project-structure records/assets.
- The template must explicitly support `WASM PWA`, offline-ready assets/service worker expectations, keyboard controls, scoring, game-over state, pause/restart, and browser proof.

## Non-software templates

Business/customer/legal/incident/research templates need typed operation contracts too. Examples:

- Intake/briefing: `ReadProcessContext`, `WriteManagedProcessArtifacts`, target scope `ManagedProcessArtifactsOnly`.
- Review/approval: `ReadProcessContext`, `ReadUpstreamArtifacts`, `EscalateOrDecide`, `WriteManagedProcessArtifacts`.
- External action: only when the step actually sends/records an external commitment.
- MutateProductTarget: only for real product/work-output mutation, not decision documents.
