# Current State

The current branch has made meaningful progress in three boundaries:

1. MAF no longer owns process/project/image product tool construction.
2. Dispatcher execution calls are behind process-owned execution snapshots.
3. Artifact projection writes are now routed through storage-backed and record-only coordinators.

The largest remaining dispatcher risk is validation logic. `ArtifactValidation.cs` still owns heterogeneous rule families: expected artifact matching, project-structure governed path parsing, provider-native visual artifact scoring, managed narrative fallback, text content signal matching, placeholder detection, quality proof interpretation, and project-structure requirement preservation.

These rules are likely to become future Process Core or driver-facing concepts, but they must first be isolated as process-module-local helpers with behavior parity tests.
