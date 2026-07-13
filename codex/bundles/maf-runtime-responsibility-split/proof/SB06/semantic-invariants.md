# SB06 Semantic Invariants

- Invariant ID: `SB06-I01`
- Source raw note: `N003`.
- Expected behavior: Required-finalizer repair, JSON normalization, streamed capture, and effective invocation selection are owned by `MafFinalizerDriver`.
- Disallowed shallow implementation: Accepting assistant prose as final output or silently succeeding when required finalizer validation fails.
- Failing-first test: N/A - refactor/characterization extraction; process/no production behavior was added.
- Passing test: `AgentFinalizerPolicyTests` finalizer coverage in `bundle://proof/SB06/transcripts/validation.txt`.
- Changed source files: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafFinalizerDriver.cs`.
- Production assertions: Runtime delegates finalizer repair, JSON repair, streamed capture, and effective invocation selection to the driver.
- Red-team negative case: Missing, malformed, or duplicated required finalizer invocations must still fail through explicit validation.
- Downstream dependency check: SB08 process and workflow browser routes render after the finalizer split.
