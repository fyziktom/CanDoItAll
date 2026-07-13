# SB05 Semantic Invariants

- Invariant ID: `SB05-I01`
- Source raw note: `N008`.
- Expected behavior: Context manifest creation and schema estimates are owned by `MafContextManifestBuilder` without changing totals or ordering.
- Disallowed shallow implementation: Keeping manifest construction in runtime or changing manifest source ordering.
- Failing-first test: N/A - refactor/characterization extraction; process/no production behavior was added.
- Passing test: MAF build and focused runtime tests in `bundle://proof/SB05/transcripts/validation.txt`.
- Changed source files: `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafContextManifestBuilder.cs`.
- Production assertions: Runtime execution and capability reporting call the manifest builder.
- Red-team negative case: Tool schema estimates must stay deterministic and not depend on provider state.
- Downstream dependency check: SB07 source scan confirms runtime delegates manifest construction.
