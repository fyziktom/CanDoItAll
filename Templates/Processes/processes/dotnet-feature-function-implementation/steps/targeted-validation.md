# Run focused validation

Run the agreed proof and record commands, exit codes, relevant output, and screenshots when UI changed.

This step owns the feature validation branch decision:

- Select `feature-accepted` only when the focused proof satisfies the accepted behavior.
- Select `feature-repair-required` when proof fails, artifacts are missing, the app cannot launch, implementation is incomplete, or evidence does not map to the accepted behavior.
- Return a completed process-step outcome with the selected branch outcome. Do not return `Blocked` only because product proof failed and can be repaired by the implementation role.
- Return `Blocked` only when an environment, permission, unavailable tool, or process-contract issue prevents validation or prevents repair from being requested inside this subprocess.

Keep focused validation bounded. Use a `workspace_dotnet_test` timeout of 300 seconds or less for generated or focused tests unless a current diagnostic proves the test suite needs more time. If validation hangs or times out, record the command and timeout as failing proof and request targeted rework instead of waiting on a broad unbounded run.

For UI proof, launch or verify the app before opening a browser:

- If no trusted launch receipt exists in the current run, call `workspace_dotnet_run` for the product app project and wait for the HTTP endpoint.
- Navigate only to the URL returned by the launch receipt, or to a URL confirmed by an explicit running-app check.
- Do not hard-code localhost ports. A guessed URL is not proof.
- Capture browser snapshot, console messages, and screenshot after successful navigation.
- If launch fails because of the product implementation, record the launch command, exit code, logs, and blocker, then select `feature-repair-required`.
