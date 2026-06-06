# SB28 Semantic Invariants

- Invariant ID: SB28-INV-001
- Source raw note: Preserve claim lease, renew, held-check and release semantics.
- Expected behavior: Dispatch claim storage, route ordering, heartbeat renewal, and failure closure remain equivalent while DispatchAsync delegates named responsibilities.
- Disallowed shallow implementation: Moving code into anonymous helpers, adding Process Core or driver APIs, changing route order, or removing failure and release paths.
- Failing-first test: N/A for process/non-production boundary refactor; negative coverage comes from command transcript bundle://proof/SB28/transcripts/source-boundary-scan.txt.
- Passing test: dotnet build, focused unit tests, and focused integration tests are recorded under bundle://proof/SB28/transcripts/.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchClaimLease.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExceptionClosure.cs, and repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs.
- Production assertions: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchClaimLease.cs owns EF claim writes; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs owns route flow; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExceptionClosure.cs owns failure closure; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs has no direct claim write tokens.
- Red-team negative case: bundle://proof/SB28/transcripts/anti-stub-scan.txt proves no stubs, TODOs, not-implemented paths, Process Core references, or driver API tokens in changed production dispatch files.
- Downstream dependency check: bundle://proof/SB28/transcripts/source-boundary-scan.txt and focused tests cover route order, finalizer handoff, claim boundary placement, and UI no-change scan.
