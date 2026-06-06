# SB032 Proof Manifest

## Changed File Hashes

- SHA256 65191BFF97D3979B5630A9764AF8C94A03171320836244109A09B7A730F9A0CF repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs
- SHA256 7372095A1AF7525075304ECD1D1221FCF142AED5FC22C6C3D59C2026D5401310 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs
- SHA256 1F44827F20DF86A5B3BBDF678B12A9B1B1DCEA24CAEC658FA59BE90FBB570549 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePipeline.cs
- SHA256 66F8BAE6AEBD9165A9427E437CBD063009A549899B5CAAD14FD71989D4C897BE repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePlanner.cs
- SHA256 710B9BA745F700FE2A5CDB58E7B0219DD9618CBB2BEBF91B31A604B05687FC3B repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchClaimLease.cs
- SHA256 565137916F7155B9904B0B4909DE5BCD8908CE7069752A3454D0B7D7DFBCBAF0 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExceptionClosure.cs
- SHA256 415032592FA58452B404B5DD06DE77EF79F3B7ABF67737AD003709537034F608 repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs
- SHA256 003F9F37539EBE22E1FCA63B6D95174713ECEE9523350C636C8E89E0BBAE0CA6 repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs

## Command Transcripts

- Failing-first transcript: `bundle://proof/transcripts/failing-first-route-handler-boundary.txt`
- Passing build transcript: `bundle://proof/transcripts/build-slnx.txt`
- Passing unit test transcript: `bundle://proof/transcripts/unit-route-boundary-tests.txt`
- Passing integration test transcript: `bundle://proof/transcripts/integration-route-boundary-tests.txt`
- Source scan transcript: `bundle://proof/transcripts/source-boundary-scan.txt`
- Anti-stub audit transcript: `bundle://proof/transcripts/anti-stub-scan.txt`
- Line-count transcript: `bundle://proof/transcripts/line-count-review.txt`
- Hash transcript: `bundle://proof/transcripts/changed-file-hashes.txt`

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteExecution.cs` keeps claim lifetime, heartbeat handling, candidate hydration, and delegates route stages through `CreateClaimedDispatchRouteHandlerPipeline`.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` defines `IProcessClaimedDispatchRouteHandler`, `ProcessClaimedDispatchRouteContext`, `ProcessDispatchRouteHandlerPipeline`, `ProcessDispatchRouteOrderAssertion`, and the eleven named route handlers.
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePipeline.cs` remains the canonical route stage order.
- `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` proves the source boundary and named side-effect handlers.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs` proves route planner, route stage order, database blocker, and finalizer composition source coverage.

## Semantic Adequacy Gate

- Raw note owned: `bundle://inputs/00-original-request.md` requires smaller dispatcher isolation, no Process Core, no production driver APIs, behavior preservation, and individual subbundle proof.
- Shallow-pass trap: a wrapper-only refactor could leave route-stage decisions in `ExecuteClaimedDispatchRouteAsync` while adding empty handler types.
- Adversarial negative proof: `bundle://proof/transcripts/failing-first-route-handler-boundary.txt` checks the pre-refactor `HEAD` route body for the handler pipeline and returns `ExitCode: 1`.
- Semantic positive proof: `bundle://proof/transcripts/unit-route-boundary-tests.txt` and `bundle://proof/transcripts/integration-route-boundary-tests.txt` pass against the named route handler pipeline and canonical stage order.
- Anti-stub audit: `bundle://proof/transcripts/anti-stub-scan.txt` reports no `TODO`, `NotImplemented`, `throw new Exception`, `stub`, or `placeholder` markers in changed production route dispatch files.
- Raw-note literal closure: no Process Core, no driver API, no UI/mobile/browser proof drift, exact route order, and individual report rows are closed by source scans and execution report rows.
- Semantic invariant contract: `bundle://proof/SB032/semantic-invariants.md`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Claimed dispatch route handler order | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` | Pipeline validates handler stage order against `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePipeline.cs` before execution. | `bundle://proof/transcripts/failing-first-route-handler-boundary.txt` |
| Direct-agent execution outcome context | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` | `DirectAgentExecutionRouteHandler` records the outcome before competing, run-closed, and finalizer handlers can consume it. | `bundle://proof/transcripts/unit-route-boundary-tests.txt` |

## Proof Claim To Code Matrix

| Capability claim | Required production source proof | Required test proof | Required negative fixture | Result |
| --- | --- | --- | --- | --- |
| Route stages execute through named module-local handlers in canonical order. | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs` and `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePipeline.cs` | `bundle://proof/transcripts/unit-route-boundary-tests.txt`, `bundle://proof/transcripts/integration-route-boundary-tests.txt` | `bundle://proof/transcripts/failing-first-route-handler-boundary.txt` | Passed |
| Process Core and production driver APIs remain absent. | `bundle://proof/transcripts/source-boundary-scan.txt` | `bundle://proof/transcripts/build-slnx.txt` | `bundle://proof/transcripts/source-boundary-scan.txt` | Passed |
