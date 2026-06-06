# SB024 Semantic Invariants

- Invariant ID: SB024-INV-001
- Source raw note: Preserve behavior while preparing Process Core and driver readiness without creating either production surface.
- Expected behavior: The model readiness proof gate remains module-local, typed, and validated by build, focused tests, route-order scan, and source scans.
- Disallowed shallow implementation: Renaming files or moving adapter calls without preserving route order, claim checks, materialization facts, or finalizer/failure closure behavior is rejected.
- Failing-first test: N/A process proof gate; shallow proof is rejected by bundle://proof/SB027/transcripts/source-scan.txt and focused architecture tests.
- Passing test: dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~ProcessAgentExecutionBoundaryArchitectureTests in bundle://proof/SB027/transcripts/unit-architecture-tests.txt and focused dispatch integration proof in bundle://proof/SB027/transcripts/integration-dispatch-tests.txt.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFailureClosureService.cs.
- Production assertions: Route services do not reference dispatcher adapters, pre-execution services consume route materialization facts, route runtime collaborators own dispatcher-edge conversion, and failure closure owns terminal exception transition checks.
- Red-team negative case: bundle://proof/SB027/transcripts/source-scan.txt rejects Process Core creation, production process-driver API tokens, UI proof drift, stub markers, adapter leakage in route services, and route-order drift.
- Downstream dependency check: bundle://proof/SB027/transcripts/build-slnx.txt, bundle://proof/SB027/transcripts/unit-architecture-tests.txt, and bundle://proof/SB027/transcripts/integration-dispatch-tests.txt passed after all edits.
