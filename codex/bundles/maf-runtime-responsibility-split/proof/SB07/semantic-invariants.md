# SB07 Semantic Invariants

- Invariant ID: `SB07-I01`
- Source raw note: `N001`, `N002`, `N006`.
- Expected behavior: `MafAgentRuntime` is an orchestrator that delegates helpers, builders, context manifest, and finalizer work to focused internal collaborators.
- Disallowed shallow implementation: Moving code to another monolithic helper or adding partial files that still mix unrelated runtime responsibilities.
- Failing-first test: N/A - refactor/characterization extraction; process/no production behavior was added.
- Passing test: MAF build and focused unit command in `bundle://proof/SB07/transcripts/validation.txt`.
- Changed source files: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`.
- Production assertions: Source scan shows runtime calls `MafRuntimeSessionBuilder`, `MafModelParametersBuilder`, `MafContextManifestBuilder`, `MafFinalizerDriver`, and `MafToolInvocationArgumentFormatter`.
- Red-team negative case: A new catch-all helper would fail the source-scan and responsibility proof.
- Downstream dependency check: SB08 live app browser proof passed after runtime slimming.
