# Hand off repaired setup evidence

Summarize the repaired setup path for the parent development slice.

## Evidence rules

- Read the required upstream repair artifact exactly as listed in the step brief. The canonical managed artifacts are `artifacts/process-runs/<setup-run-id>/steps/repair-solution-setup.md` and `artifacts/process-runs/<setup-run-id>/steps/validate-first-build-after-repair.md`.
- Return `Completed` when repaired validation selected `setup-validated` and the repaired first-build artifact reports successful restore, build, and test or discovery commands.
- Return `Blocked` only when required managed refs are unreadable in the current run, repaired validation did not select `setup-validated`, or a required created path or command result cannot be verified.

## Output

Write the repaired setup handoff packet to `artifacts/process-runs/<current-process-run-id>/steps/setup-handoff-after-repair.md` and include that exact path plus the validated upstream repaired first-build artifact in `evidenceRefs`.
