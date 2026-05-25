# SB05 Semantic Invariants

## SB05-INV-001

- Invariant ID: `SB05-INV-001`
- Source raw note: N004, N007
- Expected behavior: a step blocked for missing upstream artifacts remains blocked until the required artifact is materialized, then reopens deterministically.
- Disallowed shallow implementation: marking dependents ready on upstream completion alone or retrying a missing-input block before materialization.
- Failing-first test: `bundle://proof/SB05/transcripts/failing-first.txt`
- Passing test: `bundle://proof/SB05/transcripts/passing.txt`
- Changed source files and hashes: `bundle://proof/SB05/transcripts/changed-file-hashes.txt`
- Production assertions: `bundle://proof/SB05/transcripts/source-assertions.txt`
- Red-team negative case: downstream implementation does not retry when the upstream artifact is still absent.
- Downstream dependency check: SB08 no-progress compression can distinguish missing-input blocks from retryable work failures.

## Production Behavior Artifact Matrix

| Artifact/signal | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| `MissingUpstreamArtifactMaterializationResolved` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeProgressionPlanner.cs` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeEventTypes.cs` | `bundle://proof/SB05/transcripts/failing-first.txt` |
