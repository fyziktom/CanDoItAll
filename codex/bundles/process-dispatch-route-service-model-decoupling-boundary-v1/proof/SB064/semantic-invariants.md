# SB064 Semantic Invariants

- Invariant ID: ROUTE-SERVICE-MODEL-DECOUPLING-INV-001
- Source raw note: RAW-001 through RAW-006 require incremental route isolation, behavior preservation, no Process Core, no production driver APIs, no UI/mobile proof, and no collapsed execution rows.
- Expected behavior: route handlers and route facets use route-owned candidate, claim, execution context, and direct-agent outcome models while preserving canonical route order and existing dispatch behavior.
- Disallowed shallow implementation: merely renaming ProcessDispatchRouteServices or keeping dispatcher nested model aliases in route-facing files would not satisfy the boundary.
- Failing-first test: undle://proof/SB128/transcripts/adversarial-negative-forbidden-route-facing-model-scan.txt is the adversarial negative source scan; it returns non-zero if forbidden route-facing model tokens are absent.
- Passing test: undle://proof/SB128/transcripts/unit-route-boundary-test.txt, undle://proof/SB128/transcripts/unit-existing-route-boundary-tests.txt, and undle://proof/SB128/transcripts/integration-route-tests.txt pass.
- Changed source files: undle://proof/SB128/changed-file-hashes.md cites the changed source and test files with SHA-256 hashes.
- Production assertions: epo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs is the explicit bridge; epo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs contains narrow route services; epo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlerFactory.cs consumes ProcessDispatchRouteFacetSet.
- Red-team negative case: undle://proof/SB128/transcripts/adversarial-negative-forbidden-route-facing-model-scan.txt rejects route-facing dispatcher nested candidate, claim, and outcome type leakage.
- Downstream dependency check: undle://proof/SB128/transcripts/integration-route-tests.txt confirms route snapshot/planner/order behavior after the service/model boundary change.
