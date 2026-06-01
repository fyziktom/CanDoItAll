# QA Prompt

Review the process template changes as a contract, not as prose.

Check:

- `software-delivery` classifies .NET app type and routes implementation through subprocesses.
- Architecture design and review are split and non-mutating.
- QA, screenshot, runtime-command, release, and post-release steps cannot mutate product files.
- UI screenshots target `Screenshots` under the process run node.
- Runtime command nodes target `Run command` under the process run node and include `Run app` plus `Run tests`.
- Default process imports can resolve child subprocess definitions.
- The actual software-delivery process was not run.

Record exact tests and source assertions. If validation cannot run, state the exact command and blocker.
