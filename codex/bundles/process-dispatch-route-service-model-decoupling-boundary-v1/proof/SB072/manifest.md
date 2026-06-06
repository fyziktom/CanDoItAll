# SB072 Proof Manifest

Status: Completed.

Invariant ID: ROUTE-SERVICE-MODEL-DECOUPLING-INV-001

## Changed-file hashes

- Hash table: undle://proof/SB128/changed-file-hashes.md
- Primary changed source: epo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs
- Adapter source: epo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs
- Narrow service source: epo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs
- Factory source: epo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlerFactory.cs
- Architecture test source: epo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs

## Command transcripts

- Build transcript: undle://proof/SB128/transcripts/build-solution.txt
- Passing unit transcript: undle://proof/SB128/transcripts/unit-route-boundary-test.txt
- Passing existing route boundary transcript: undle://proof/SB128/transcripts/unit-existing-route-boundary-tests.txt
- Passing integration transcript: undle://proof/SB128/transcripts/integration-route-tests.txt
- Source boundary scan transcript: undle://proof/SB128/transcripts/source-boundary-scan.txt
- No-Core/no-driver scan transcript: undle://proof/SB128/transcripts/no-core-no-driver-src-scan.txt
- No UI/mobile scan transcript: undle://proof/SB128/transcripts/no-ui-mobile-diff-scan.txt
- Anti-stub audit transcript: undle://proof/SB128/transcripts/anti-stub-audit.txt

## Semantic proof

- Semantic invariant contract: undle://proof/SB072/semantic-invariants.md
- Shipped behavior: route handlers and facets consume ProcessRouteCandidate, ProcessRouteDispatchClaim, and ProcessRouteExecutionOutcome; dispatcher nested types are bridged only through ProcessDispatchRouteModelAdapters.
- Source proof: epo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteFacets.cs, epo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs, epo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteHandlerFactory.cs.
- Adversarial negative proof: undle://proof/SB128/transcripts/adversarial-negative-forbidden-route-facing-model-scan.txt rejects dispatcher-owned nested model usage in route-facing files.
- Semantic positive proof: undle://proof/SB128/transcripts/unit-route-boundary-test.txt and undle://proof/SB128/transcripts/integration-route-tests.txt verify model boundary and route behavior preservation.
- Anti-stub audit: undle://proof/SB128/transcripts/anti-stub-audit.txt reports no stubs or placeholder exceptions.
