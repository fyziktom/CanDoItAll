# Validate first build and test discovery

Run restore/build and a targeted test command or discovery command. Capture the command, exit code, warnings, and any remaining setup risks.

## Branching

- Return `Completed` with branch outcome `setup-validated` only when restore, build, and targeted test discovery or initial test command are green enough for parent implementation.
- Return `Completed` with branch outcome `setup-repair-required` for repairable scaffold, restore, build, package, reference, template-integrity, or test-discovery failures. Include the exact failing command, exit code, relevant output, expected repair target, and product paths.
- Return `Blocked` only when an environment, permission, missing tool, or process-contract issue prevents validation evidence collection or branch routing.

Keep this as setup validation. Do not launch runtime, start a web app, run browser proof, edit feature behavior, or replace generated starter UI/content. Runtime and browser proof belong to downstream validation steps unless the parent process defines a separate runtime-proof setup step.
