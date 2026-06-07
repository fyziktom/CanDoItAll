# SB009 Semantic Invariants

- Invariant ID: `SB009-ROUTE-BEHAVIOR-PARITY`
- Source raw note: Preserve functionality while extracting only route-stage and eligibility rules.
- Expected behavior: Stage order and route decisions remain unchanged after the namespace/project move.
- Disallowed shallow implementation: Reordering route stages, dropping eligibility checks, or replacing dispatch decisions with permissive fallback behavior.
- Failing-first test: N/A process/no production behavior; route drift is rejected by the architecture suite and dispatch integration suite.
- Passing test: bundle://proof/common/transcripts/unit-architecture.txt and bundle://proof/common/transcripts/integration-dispatch.txt
- Changed source files: repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRoutePipeline.cs, repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRoutePlanner.cs, and repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRouteSnapshot.cs
- Production assertions: Existing dispatch handlers still use the same route kinds and stage order through the module adapter.
- Red-team negative case: bundle://proof/common/transcripts/anti-stub-scan.txt rejects placeholder route code.
- Downstream dependency check: bundle://proof/common/transcripts/build-solution.txt proves all consumers compile against the moved types.
