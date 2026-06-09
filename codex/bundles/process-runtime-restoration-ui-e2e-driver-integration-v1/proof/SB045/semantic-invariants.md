# SB045 Semantic Invariants

Status: Passed.

## Shallow-Pass Trap

This gate is not satisfied by proving a `.NET` scenario and a business-analysis scenario both pass. Those are scenario proofs. Gate O must prove Process Core stays generic: no software-only, Office-only, business-only, driver-package, process-module, infrastructure, workspace/storage, EF, DI, or AgentFramework dependencies in the Core source or project.

## Adversarial Negative Proof

The proof would fail if any of these regressions were introduced:

- Process Core source contains `.NET`, `DotNetRust`, software-development, Office/Graph/Gmail, CRM, business-analysis, or business-plan domain logic;
- Process Core references `CanDoItAll.Processes.Drivers.*`, `CanDoItAll.Modules.*`, infrastructure, workspace/storage, EF, DI/service provider, or AgentFramework;
- Process Core project gains package references or non-contract project references;
- domain-specific verification implementation moves from driver packages or process-module read-only adapters into Core;
- the guard scans generated `bin`/`obj` source and treats SDK-generated `.NETCoreApp` attributes as product leakage;
- a test passes by deleting generic Core descriptors instead of preserving them.

## Semantic Positive Proof

`bundle://proof/SB043/transcripts/core-domain-leakage-source-scan.txt` proves non-build Core C# source and Core project references are clean.

`bundle://proof/SB044/transcripts/domain-specific-boundary-source-assertions.txt` proves domain-specific verification implementation remains outside Core.

`bundle://proof/SB045/transcripts/focused-core-genericity-architecture-test.txt` proves the executable Gate O guard passes.

## Anti-Stub Proof

`bundle://proof/SB045/transcripts/anti-stub-core-genericity-negative-proof.txt` proves a synthetic leaky Core source would be rejected. A report-only closure, non-empty scan output, or deleted Core surface cannot satisfy Gate O.

## Raw-Note Closure

- RN-006 is partially solved: Gate O proves Process Core remains generic while domain-specific verification stays in driver/adapters. Runtime-host approval and broader driver evolution remain future-gated by SB042.
- RN-009 remains partially solved: SB001-SB045 now have separate gate rows through Core genericity; remaining release, docs, and final gates still need execution.

## Production Behavior Artifact Matrix

No production runtime behavior was added. Gate O creates executable architecture proof only.

| Artifact | Producer | Consumer | Lifecycle |
| --- | --- | --- | --- |
| Core genericity architecture guard | `repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs` | Unit test runner | Runs as regression proof that Process Core remains generic and dependency-light. |
| Core/domain boundary proof transcripts | `bundle://proof/SB043/transcripts/core-domain-leakage-source-scan.txt`; `bundle://proof/SB044/transcripts/domain-specific-boundary-source-assertions.txt` | Gate O manifest/review | Updated when source moves between Core, process-module adapters, or driver packages. |
