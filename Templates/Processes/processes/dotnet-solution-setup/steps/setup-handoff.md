# Hand off setup evidence

Summarize created paths, commands run, validation status, and blockers for the parent development slice.

## Evidence rules

- Read the required upstream first-build artifact exactly as listed in the step brief. The canonical managed artifact is `artifacts/process-runs/<setup-run-id>/steps/validate-first-build.md`.
- A successful `workspace_stat_path` or `workspace_read_file` receipt for that exact `steps/validate-first-build.md` ref is durable first-build evidence. Do not require or probe a sibling path such as `artifacts/process-runs/<setup-run-id>/validate-first-build`.
- Return `Completed` when the canonical first-build artifact is readable and reports successful restore, build, and test or discovery commands, unless it explicitly records unresolved setup blockers.
- Return `Blocked` only when the exact required managed ref is unreadable in the current run, the artifact content reports a failed setup validation, or a required created path/command result cannot be verified.

## Output

Write the setup handoff packet to `artifacts/process-runs/<current-process-run-id>/steps/setup-handoff.md` and include that exact path plus the validated upstream first-build artifact in `evidenceRefs`.
