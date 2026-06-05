# SB04 Semantic Invariants

- Invariant ID: SB04-INV-001
- Source raw note: Do not rush Process Core; keep the next dispatcher isolation step module-local.
- Expected behavior: Candidate factory and context helpers exist inside the Processes dispatch module and construct candidates without hidden side effects.
- Disallowed shallow implementation: Adding method names while leaving inline candidate construction and side-effect tokens in the helper files.
- Failing-first test: bundle://proof/SB04/transcripts/sb04-failing-first-candidate-factory-guardrail.txt
- Passing test: bundle://proof/SB04/transcripts/sb04-passing-candidate-factory-guardrail.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateAssemblyContext.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateFactory.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs
- Production assertions: Dispatcher calls ProcessDispatchCandidateAssemblyContextFactory and ProcessDispatchCandidateFactory while binding, execution-run queries, recovery loads, and logging remain outside the factory.
- Red-team negative case: SB04 failing-first transcript proves the current code failed when the factory/context boundary was absent.
- Downstream dependency check: SB08 and SB12 route parity tests depend on this helper boundary.
