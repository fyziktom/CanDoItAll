# SB015 Proof Manifest

## Result

Completed. Subprocess behavior remains module-local; only a route-kind decision is represented in Core.

## Portable References

- Subbundle: bundle://subbundles/SB015/README.md
- Semantic invariants: bundle://proof/SB015/semantic-invariants.md
- Core route planner: repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRoutePlanner.cs
- Core route snapshot: repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRouteSnapshot.cs
- Architecture guard: repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs

## Changed File SHA-256

- repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRoutePlanner.cs: 25a67bcba012e85572c33c5045f89baa2234dde7ad1563f9f34052dbb150745d
- repo://src/CanDoItAll.Processes.Core/Routing/ProcessDispatchRouteSnapshot.cs: ad441bf86f2e0c591e21a30206df13710c8d8bc4e6d4a8fa78a56db7139c888f
- repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs: d2bc3d1bb7d051eae31dd02d3f78df08ae2a5fe39a7766ca29843bae457fdc4b

## Command Transcripts

- Passing build: bundle://proof/common/transcripts/build-solution.txt
- Passing architecture test: bundle://proof/common/transcripts/unit-architecture.txt
- Passing dispatch integration test: bundle://proof/common/transcripts/integration-dispatch.txt
- Production driver-token scan: bundle://proof/common/transcripts/production-driver-token-scan.txt
- Anti-stub audit: bundle://proof/common/transcripts/anti-stub-scan.txt
- Adversarial negative proof: N/A process/no production behavior; subprocess extraction was deliberately not performed.

## Closure

Subprocess lifecycle code is still in the Processes module; Core only knows whether a route snapshot is subprocess-shaped.
