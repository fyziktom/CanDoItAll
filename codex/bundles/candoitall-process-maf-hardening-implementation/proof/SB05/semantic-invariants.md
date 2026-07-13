# Semantic Invariants - SB05

## INV-SB05-01

- Invariant ID: `INV-SB05-01`
- Source raw note: F03/F04 require runtime-owned subprocess parent bridge behavior.
- Expected behavior: parent completes only from accepted child handoff and blocks concretely on no-go or missing accepted output.
- Disallowed shallow implementation: complete from generic child step folder or ask the normal agent to relaunch a controlled child process.
- Failing-first test: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing test: `bundle://proof/SB09/transcripts/final-validation.md`
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ParentSubprocessArtifactBridge.cs`, `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.Subprocess.cs`.
- Production assertions: bridge result cases distinguish no match, active child, accepted child output, no-go output, and completed child missing accepted output.
- Red-team negative case: `setup-repair-escalation` produces a concrete no-go blocker and is not accepted as parent evidence.
- Downstream dependency check: SB08 template contracts rely on bridge behavior.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Parent synthesized handoff artifact | bridge source/test | runtime finalization/produced slot | child terminal to parent completion lifecycle | no-go child result blocks |
