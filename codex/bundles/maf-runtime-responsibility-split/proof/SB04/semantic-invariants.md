# SB04 Semantic Invariants

- Invariant ID: `SB04-I01`
- Source raw note: `N007`.
- Expected behavior: Model-compatible options, temperature omission/retry, reasoning effort diagnostics, and model resolution are built by `MafModelParametersBuilder`.
- Disallowed shallow implementation: Leaving model-parameter construction in runtime or silently dropping unsupported reasoning effort.
- Failing-first test: N/A - refactor/characterization extraction; process/no production behavior was added.
- Passing test: `AgentFinalizerPolicyTests` reasoning-effort coverage in `bundle://proof/SB04/transcripts/validation.txt`.
- Changed source files: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafModelParametersBuilder.cs`.
- Production assertions: Runtime, agent factory, and capability reporting call the model builder.
- Red-team negative case: OpenAI-like unsupported temperature messages must be recognized before a single retry without temperature.
- Downstream dependency check: SB03 and SB07 run-option construction uses the model builder.
