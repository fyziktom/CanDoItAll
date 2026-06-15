# Define focused validation contract

Map each acceptance criterion to a specific unit test, integration test, build command, or browser proof. Prefer narrow commands that diagnose failures quickly before broader regression validation.

This step writes the validation contract only. Do not run build, test, launch, or browser tools here. Missing runtime tooling is a prerequisite or risk for the later validation step; it is not a blocker for writing a contract when acceptance criteria and a target root are available.

For Blazor or other UI behavior, the contract must require the later validation step to include app launch proof before any browser proof:

- Use the product app project for the launch command when no trusted launch receipt already exists.
- Use the URL returned by the launch/run receipt for browser navigation; do not assume `http://127.0.0.1:5000/`.
- Treat browser proof as actionable only after a successful launch receipt or an explicitly verified running app.
- If the later launch fails because configuration or dependencies are missing, validation must record the command, logs, and blocker and request targeted repair before escalation.
