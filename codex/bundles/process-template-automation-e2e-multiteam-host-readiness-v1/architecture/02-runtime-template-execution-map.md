# Runtime Template Execution Map

The next implementation must distinguish these proof levels:

1. **Catalog proof**: template exists and can be projected/imported.
2. **Manual runtime proof**: steps are transitioned by tests and artifacts are recorded manually.
3. **Automation runtime proof**: run is started, outbox is claimed, dispatch executes route, finalizer closes step, artifacts are projected/read back.
4. **User-facing proof**: UI/project/project-structure path creates or selects the run and readback displays status/artifacts/diagnostics.
5. **Live provider proof**: opt-in live OpenAI process-run path with bounded model/timeout/token budget.

Current state has strong catalog/manual proof for some templates. This bundle must upgrade representative templates to automation runtime proof and, where appropriate, user-facing proof.
