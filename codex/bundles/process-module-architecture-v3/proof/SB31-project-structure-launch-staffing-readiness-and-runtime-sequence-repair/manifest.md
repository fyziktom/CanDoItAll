# SB31 Proof Manifest

## Scope

Project-structure process launch repair for the user-reported `23505` duplicate `process_runtime_events.GlobalSequence` failure, incorrect HR/staffing recommendations, manual override bypass, and empty-output-root Tetris greenfield classification block.

## Product Changes

- Added `AgentProcessReadinessEvaluator` to score role-family fit and workspace operation readiness from typed agent metadata, workspace tool profiles, step allowed operations, and step target scope.
- Routed automatic resolver selection and project-structure manual executor overrides through the same readiness gate.
- Added launch-plan step operation metadata so the UI can evaluate candidates with the same operation contract shown to the resolver.
- Updated project-structure assignment candidates to hide weak/unready candidates from recommendations and mark unready selections unresolved.
- Added a PostgreSQL operation migration that repairs the `process_runtime_events.GlobalSequence` identity sequence with `setval(max + 1, false)`.
- Expanded .NET launch-variable enrichment to architecture, development, and solution-setup subprocesses so greenfield targets carry `DotNetScaffoldContract`, app archetype, template, project directories, test framework, and target framework.
- Updated the .NET architecture classification template so an empty grounded product/output root is greenfield repository state, not a hard blocker when typed launch facts identify the app and layout.

## Validation

- `transcripts/test-unit-process-launch-executor-resolver.txt`: passed 3/3.
- `transcripts/test-components-project-structure-assignment-dialog.txt`: passed 5/5.
- `transcripts/test-integration-project-structure-agent.txt`: passed 26/26.
- `transcripts/build-solution.txt`: solution build passed with 0 warnings and 0 errors.
- `transcripts/git-diff-check.txt`: no whitespace errors; only line-ending normalization warnings.
- `runtime-smoke-tetris-start-response.json`: live dev-DB Tetris launch created run `1adde2cb-d093-4a4c-9173-dd286ca466c2` with readiness OK and no duplicate sequence failure.

## Live Runtime Notes

The live smoke intentionally stopped at run creation/launch-plan verification. It proves the failing start path now succeeds and HR readiness binds executable steps without launch blockers. Full provider execution was not allowed to run unbounded after the greenfield repair; the greenfield classifier/subprocess prompt path is covered by integration proof.
