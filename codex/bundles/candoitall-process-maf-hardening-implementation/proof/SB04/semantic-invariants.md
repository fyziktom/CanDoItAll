# Semantic Invariants - SB04

## INV-SB04-01

- Invariant ID: `INV-SB04-01`
- Source raw note: F03/F04/F09 require typed subprocess contracts rather than prose-only launch and handoff rules.
- Expected behavior: subprocess parent steps load machine-readable contract metadata for launch mode, accepted child outputs, no-go outputs, and materialization mode.
- Disallowed shallow implementation: parsing markdown prose to infer child handoff success.
- Failing-first test: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing test: `bundle://proof/SB09/transcripts/final-validation.md`
- Changed source files: `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessSubprocessContracts.cs`, `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeLaunchVariables.cs`, `repo://src/Processes/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs`.
- Production assertions: template loader and launch variables round-trip typed `ProcessSubprocessContract`.
- Red-team negative case: no-go escalation output cannot satisfy an accepted child handoff mapping.
- Downstream dependency check: SB05 bridge and SB08 template hardening depend on typed contract semantics.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessSubprocessContract` | template loader and contract source | bridge/resolver/launch variables | template load to assignment lifecycle | prose-only hard gate is rejected |
