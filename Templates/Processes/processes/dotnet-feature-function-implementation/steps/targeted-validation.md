# Run focused validation

Run the agreed proof and record commands, exit codes, relevant output, and screenshots when UI changed. If proof fails because work or artifacts are missing, request targeted rework through the manager before escalation.

For UI proof, launch or verify the app before opening a browser:

- If no trusted launch receipt exists in the current run, call `workspace_dotnet_run` for the product app project and wait for the HTTP endpoint.
- Navigate only to the URL returned by the launch receipt, or to a URL confirmed by an explicit running-app check.
- Do not hard-code localhost ports. A guessed URL is not proof.
- Capture browser snapshot, console messages, and screenshot after successful navigation.
- If launch fails, record the launch command, exit code, logs, and blocker; ask the manager for targeted repair before escalation.
