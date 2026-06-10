# SB021 Semantic Invariants

## SB021_INV_001 Exact Typed Lane Selection
- Source raw note: "Move toward generic process driver runtime host" without approving execution-capable drivers.
- Expected behavior: the selector returns a typed exact result that distinguishes selected lanes, unsupported enum values, and defined-but-unregistered lanes.
- Disallowed shallow implementation: a boolean `TrySelect` result that forces the host to infer failure semantics or silently route to a different lane.
- Positive proof: `Process_verification_lane_selector_SB019_INV_001_returns_exact_selection_result` in `bundle://proof/SB019/transcripts/selector-hardening-focused-tests.txt`.
- Source proof: `bundle://proof/SB019/transcripts/selector-result-source-assertions.txt`.
- Changed source: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationLaneRegistry.cs` SHA256 `c8845992d1db5e2425f23db6b45a9d72f6d69307b35f1b415599124dd42e1a96`.
- Red-team negative case: `bundle://proof/SB021/transcripts/red-team-selector-hardening-shallow-proof-rejection.txt`.
- Downstream dependency check: P08 durable audit and P09 manager APIs must consume host results without fallback or reflective lane selection.

## SB021_INV_002 No Fallback, Discovery, Reflection, Or Dynamic Dispatch
- Source raw note: REQ-008 "Harden registry and selector: exact lane, no fallback, no discovery."
- Expected behavior: verification host selection is constrained to the explicit lane registry and cannot discover runtime handlers, reflect over assemblies, dispatch through `dynamic`, or use generic object payload routing.
- Disallowed shallow implementation: a passing happy-path lane test with reflective discovery, fallback registration, or broad Dispatch-folder proof that hides unrelated fallback code.
- Positive proof: `Process_verification_runtime_host_SB020_INV_001_denies_defined_but_unregistered_lane_without_fallback` in `bundle://proof/SB019/transcripts/selector-hardening-focused-tests.txt`.
- Source proof: `bundle://proof/SB020/transcripts/no-fallback-discovery-reflection-source-assertions.txt`.
- Anti-stub audit: `bundle://proof/SB021/transcripts/gate-g-source-diff-and-anti-stub-audit.txt`.
- Changed source: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs` SHA256 `18ff6fe45fd0fab1cef8eb3c91e611aeaafe86143c571aa8e6b55c41093c4bf8`.
- Red-team negative case: `bundle://proof/SB021/transcripts/red-team-selector-hardening-shallow-proof-rejection.txt`.
- Downstream dependency check: Process Core remains untouched and execution-capable driver registration remains out of scope.

## Production Behavior Artifact Matrix
| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessVerificationLaneSelectionResult` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationLaneRegistry.cs` | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs` | `bundle://proof/SB019/transcripts/selector-hardening-focused-tests.txt` | `bundle://proof/SB020/transcripts/no-fallback-discovery-reflection-source-assertions.txt` |
| `MissingLaneRegistration` denial | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs` | `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs` | `bundle://proof/SB019/transcripts/selector-hardening-focused-tests.txt` | `bundle://proof/SB021/transcripts/red-team-selector-hardening-shallow-proof-rejection.txt` |

## Gate Result
Gate G is semantically adequate for P07. The selector exposes exact typed lane results, the host denies missing registrations explicitly, focused tests pass, and production source scans reject fallback, discovery, reflection, dynamic dispatch, generic object payload routing, and stubs in the selector/host boundary.
