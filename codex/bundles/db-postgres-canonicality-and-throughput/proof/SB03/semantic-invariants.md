# SB03 semantic invariants

## SB03-I7 activation is restart-first

- Source raw note: remove dead live switch/drain semantics unless a real operator-only maintenance path is implemented.
- Expected behavior: normal runtime context creation cannot be blocked by switch sessions or drain locks.
- Disallowed shallow implementation: leaving unused switch/drain APIs that imply hot switching still exists.
- Passing transcript: `bundle://proof/SB03/transcripts/residue-and-switch-audit-final.txt`.
- Changed source files and hashes: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`.
- Production assertions: runtime database state is metadata-only in `repo://src/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs`.
- Red-team negative case: `bundle://proof/SB08/transcripts/anti-stub-audit.txt`.
- Downstream dependency check: `bundle://proof/SB08/transcripts/semantic-invariant-index.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Restart-first activation result | `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` | `repo://src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSwitchingAbstractions.cs` | `bundle://proof/SB02/transcripts/unit-runtime-switch-tests.txt` | `bundle://proof/SB03/transcripts/residue-and-switch-audit-final.txt` |
