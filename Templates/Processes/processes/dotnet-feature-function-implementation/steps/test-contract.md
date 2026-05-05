# Define focused validation contract

Map each acceptance criterion to a specific unit test, integration test, build command, or browser proof. Prefer narrow commands that diagnose failures quickly before broader regression validation.

For Blazor or other UI behavior, the contract must include the app launch proof before any browser proof:

- Use `workspace_dotnet_run` against the product app project when no trusted launch receipt already exists.
- Use the URL returned by the launch/run receipt for `browser_navigate`; do not assume `http://127.0.0.1:5000/`.
- Browser proof is only actionable after a successful launch receipt or an already-running app has been verified.
- If the app cannot launch because configuration or dependencies are missing, block with the missing environment detail instead of attempting browser tools against a guessed URL.
