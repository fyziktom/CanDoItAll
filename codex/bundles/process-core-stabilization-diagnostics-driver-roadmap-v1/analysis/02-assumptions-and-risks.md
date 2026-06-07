# Assumptions And Risks

## Assumptions
- Codex will work on branch `maf-processes-refactor`.
- The goal is still refactoring and architecture hardening, not feature expansion.
- The first Core seed and pure-rule expansion are accepted unless new tests prove otherwise.
- Production process driver APIs are still not approved for runtime use.
- Browser/mobile/small/medium proof is out of scope unless UI files unexpectedly change.

## Critical Path Risks
1. **Core surface creep**: additional pure rules could accidentally pull in process-module or infrastructure dependencies.
2. **Adapter leakage**: route/artifact/subprocess adapters could become new hidden mini-dispatchers.
3. **Warning normalization**: `CA1416` warnings may be ignored because build passes.
4. **Driver API overreach**: driver-readiness work could accidentally become production API/DI/runtime wiring.
5. **False confidence from docs-only phases**: proposal documents must be backed by negative source scans.

## Validation Risks
- Full integration project may be too slow; focused integration proof must be well chosen.
- Source scans must inspect production source, not only bundle docs.
- Unit architecture tests should fail if Core gets side-effect dependencies or if driver tokens appear in production source.

## Reopen Triggers
- Any Core project reference beyond `CanDoItAll.Processes.Contracts`.
- Any Core source containing EF, DbContext, workspace/storage/filesystem, AgentFramework, finalizer application, claim lifecycle, DI, driver API, or runtime selector tokens.
- `dotnet build` warnings increase or `CA1416` warnings remain undocumented after SB003.
- Any production `IProcessDriver*`, `DriverRegistry`, `DriverPack`, `ProcessDriverRegistry`, or runtime driver selector is introduced.
- Any UI/media file is changed without explicit large-screen-only proof.
