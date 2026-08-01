# Define focused validation contract

Map each acceptance criterion to a specific unit test, integration test, build command, or browser proof. Prefer narrow commands that diagnose failures quickly before broader regression validation.

When `ProductAcceptanceCriteriaContract` is present, write an `Acceptance-criteria coverage` table with every criterion id. For criteria with `kind=ProductAcceptance` and `required=true`, include the owning production boundary, planned proof, and expected result; an accepted branch cannot omit one even when proof is shared. Preserve `kind=DeliveryPlanning` items in a separate nonblocking section. They require neither product proof nor human confirmation and cannot route repair or escalation unless a separate typed decision gate explicitly requests that decision.

This step writes the validation contract only. Do not run build, test, launch, or browser tools here. Missing runtime tooling is a prerequisite or risk for the later validation step; it is not a blocker for writing a contract when acceptance criteria and a target root are available.

## Plan completeness

For each criterion, identify the initial state or test data, the action or stimulus, the expected observation, the focused proof owner, and the failure signal that should route repair. When a production type, host, test project, or runtime artifact does not exist yet, plan the required seam and downstream proof prerequisite rather than inventing files, choosing a runner, guessing a layout, or blocking the contract. Keep proof planning independent of a specific template, route, port, or framework convention.

When a failure could recur after repair, record the minimum before/after evidence needed to show that the next repair is materially different. A repeated observation alone is not a repair strategy; it is a diagnostic input for the appropriate implementation specialist or manager lane.

When defining arithmetic, state-machine, or other deterministic behavior tests, ensure expected values and recorded history follow from the exact action sequence in the test. Do not combine a final result from one sequence with history or state from another.

Map each behavioral acceptance criterion to proof that executes the owning production behavior. A source-text, markup-content, selector-presence, or copied-label assertion is not proof of an interaction, state transition, calculation, persistence round trip, reload restoration, timing behavior, or error recovery. Such checks may prove content hygiene only. Require a unit or integration test through the typed production boundary for deterministic behavior and reserve browser interaction proof for the parent step when this subprocess does not own browser tools. If no executable proof exists for an acceptance-critical behavior, the contract must identify that gap and require `feature-repair-required`; it must not weaken the criterion into static text proof.

For persistence and graceful-unavailable behavior, map each required ProductAcceptance criterion to the smallest sufficient owner. A deterministic test through the typed storage adapter or coordinator is sufficient proof of graceful unavailable behavior unless the criterion explicitly requires a live storage outage in a real browser. For normal IndexedDB reload proof, the parent browser-owning step may use `browser_evaluate` to seed and inspect the declared database and object-store schema, reload the app, and verify restored UI state; it does not need to play or operate the workflow until a naturally nonzero value exists.

For UI behavior, the contract must require the later validation step to include app launch proof before any browser proof:

- Use the product app project for the launch command when no trusted launch receipt already exists.
- Use the URL returned by the launch/run receipt for browser navigation; do not assume `http://127.0.0.1:5000/`.
- Treat browser proof as actionable only after a successful launch receipt or an explicitly verified running app.
- If the later launch fails because configuration or dependencies are missing, validation must record the command, logs, and blocker and request targeted repair before escalation.
