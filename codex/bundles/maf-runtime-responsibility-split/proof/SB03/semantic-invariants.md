# SB03 Semantic Invariants

- Invariant ID: `SB03-I01`
- Source raw note: `N008`.
- Expected behavior: Session creation, restoration, prompt input, response format, and run options are built by `MafRuntimeSessionBuilder` with unchanged compatibility rules.
- Disallowed shallow implementation: Keeping session construction in runtime or silently creating fresh sessions for incompatible approval continuations.
- Failing-first test: N/A - refactor/characterization extraction; process/no production behavior was added.
- Passing test: `MafAgentRuntimeAttachmentTests` in `bundle://proof/SB03/transcripts/validation.txt`.
- Changed source files: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeSessionBuilder.cs`.
- Production assertions: Runtime execution and approval continuation call `MafRuntimeSessionBuilder`.
- Red-team negative case: Approval continuation without compatible serialized session state still throws explicitly.
- Downstream dependency check: SB06 finalizer repair and SB07 orchestration use session builder run options and input messages.
