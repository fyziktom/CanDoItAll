# SB01 Semantic Invariants

- Invariant ID: `SB01-I001`
- Source raw note: `N001 missing Tetris step-two artifacts must ask manager`.
- Expected behavior: Missing required completion artifacts ask the process manager to recover artifacts from previous step history and execution evidence.
- Disallowed shallow implementation: Rerunning the current step executor or marking the step complete without required artifact expectation ids.
- Failing-first test: Live run snapshot `bundle://evidence/live-run-9228abba-snapshot.json` plus process/non-production failing-first exemption in `proof/SB01/transcripts/failing-first-exemption.md`.
- Passing test: `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~ProcessRunAutomationDispatchServiceTests --no-restore` in `proof/SB01/transcripts/passing-test.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.CompletionArtifactRecovery.cs`, `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`, and `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs`.
- Production assertions: Runtime recovery resolves a distinct process manager technical agent from configured or assigned run manager state, records a manager directive, projects recovered artifacts through existing projection, blocks when artifacts remain missing, routes stranded or reopened in-progress missing-artifact recovery to the manager before starting another executor attempt, and executes manager recovery without inheriting implementation build/run/test proof requirements.
- Red-team negative case: Ambiguous manager-like fallback agents return no manager resolution instead of silently picking one.
- Downstream dependency check: Step transition remains gated by existing artifact expectation recording before dependent process steps can run.
