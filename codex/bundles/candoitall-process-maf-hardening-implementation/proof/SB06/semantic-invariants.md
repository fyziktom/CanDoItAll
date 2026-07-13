# Semantic Invariants - SB06

## INV-SB06-01

- Invariant ID: `INV-SB06-01`
- Source raw note: F05/F06/F07 require artifact truth, descriptors, and applied-result ledger consistency.
- Expected behavior: prompts and runtime contracts expose semantic artifact descriptors, produced artifact hashes are content-grounded, and ledger events use the applied final result.
- Disallowed shallow implementation: slot GUID-only prompts, raw-output hashes, or ledgering invalid artifacts after downgrade.
- Failing-first test: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing test: `bundle://proof/SB09/transcripts/final-validation.md`
- Changed source files: `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`, `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessRuntimeArtifactContracts.cs`, `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ManagedArtifacts.cs`.
- Production assertions: descriptor persistence, prompt rendering, readback hash computation, and migration columns are present.
- Red-team negative case: unreadable managed artifact readback blocks completion instead of producing a fake content hash.
- Downstream dependency check: SB05/SB08 consume artifact descriptors and materialization mode.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Content-grounded produced artifact ref | materializer/readback source and tests | runtime artifact ledger | materialization to produced slot lifecycle | raw-output-only hash rejected |
