# SB02 Semantic Invariants

## Invariants

- Invariant ID: SB02-INV-001
- Source raw note: RN02 - Map the current Processes runtime architecture and define service boundaries.
- Expected behavior: The Processes README describes current source-backed lifecycle ownership, runtime layers, high-risk dispatch boundaries, artifact/manager/projection services, and refactor targets.
- Disallowed shallow implementation: Leaving the README as a generic module stub or claiming boundaries that do not exist in source.
- Failing-first test: N/A - process/non-production documentation update; the previous README omitted the runtime-layer map.
- Passing test: bundle://proof/SB02/transcripts/sb02-source-assertions.txt confirms the named source surfaces exist.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/README.md
- Production assertions: No production code changed in SB02; runtime behavior remains unchanged until later subbundles.
- Red-team negative case: The README calls out high-risk partial classes and avoids claiming output grounding, manager resolution, or artifact semantics are fully refactored before SB03-SB07.
- Downstream dependency check: SB03-SB08 and SB16 can use the documented boundaries as the execution map.
