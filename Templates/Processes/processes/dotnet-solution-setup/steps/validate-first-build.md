# Validate first build and test discovery

Run restore/build and a targeted test command or discovery command. Capture the command, exit code, warnings, and any remaining setup risks.

This is an execution-backed validation step. Use the available `workspace_dotnet_restore`, `workspace_dotnet_build`, and `workspace_dotnet_test` tools against the contracted solution or product root before finalizing. The `ProductCompletionRequiredToolReceipts` launch variable is a hard validation gate for this step; successful current-run receipts for restore, build, and test are required before `Completed`. Before command execution, inspect the contracted solution file and the app/test project files. A solution with no app/test project entries is an incomplete scaffold even when `dotnet build` exits successfully as a no-op. If a command fails, or if the solution is empty/disconnected, write the evidence and return `Completed` with branch outcome `setup-repair-required`; that is a normal repair branch, not a manager escalation. Return `Blocked` for missing execution capability only after a current tool boundary or denied tool receipt proves the required validation tool is unavailable.

## Branching

- Return `Completed` with branch outcome `setup-validated` only when restore, build, and targeted test discovery or initial test command are green enough for parent implementation.
- Return `Completed` with branch outcome `setup-repair-required` for repairable scaffold, empty-solution, disconnected test project, restore, build, package, reference, template-integrity, or test-discovery failures. Include the exact failing command or readback, exit code when applicable, relevant output, expected repair target, and product paths.
- Return `Blocked` only when an environment, permission, missing tool, or process-contract issue prevents validation evidence collection or branch routing.

In the managed artifact, include the exact selected branch key on its own line as `Branch outcome key: setup-validated` or `Branch outcome key: setup-repair-required` before finalizing.

Keep this as setup validation. Do not launch runtime, start a web app, run browser proof, edit feature behavior, or replace generated starter UI/content. Runtime and browser proof belong to downstream validation steps unless the parent process defines a separate runtime-proof setup step.
