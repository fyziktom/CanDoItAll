# Define focused validation contract

Map each acceptance criterion to a specific unit test, integration test, build command, or browser proof. Prefer narrow commands that diagnose failures quickly before broader regression validation.

This step writes the validation contract only. Do not run build, test, launch, or browser tools here. Missing runtime tooling is a prerequisite or risk for the later validation step; it is not a blocker for writing a contract when acceptance criteria and a target root are available.

When defining arithmetic, state-machine, or other deterministic behavior tests, ensure expected values and recorded history follow from the exact action sequence in the test. Do not combine a final result from one sequence with history or state from another.

Map each behavioral acceptance criterion to proof that executes the owning production behavior. A source-text, markup-content, selector-presence, or copied-label assertion is not proof of an interaction, state transition, calculation, persistence round trip, reload restoration, timing behavior, or error recovery. Such checks may prove content hygiene only. Require a unit or integration test through the typed production boundary for deterministic behavior and reserve browser interaction proof for the parent step when this subprocess does not own browser tools. If no executable proof exists for an acceptance-critical behavior, the contract must identify that gap and require `feature-repair-required`; it must not weaken the criterion into static text proof.

For Blazor or other UI behavior, the contract must require the later validation step to include app launch proof before any browser proof:

- Use the product app project for the launch command when no trusted launch receipt already exists.
- Use the URL returned by the launch/run receipt for browser navigation; do not assume `http://127.0.0.1:5000/`.
- Treat browser proof as actionable only after a successful launch receipt or an explicitly verified running app.
- If the later launch fails because configuration or dependencies are missing, validation must record the command, logs, and blocker and request targeted repair before escalation.
