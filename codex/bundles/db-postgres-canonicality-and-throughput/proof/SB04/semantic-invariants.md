# SB04 semantic invariants

## SB04-I3 profile contexts are maintenance-only

- Source raw note: profile-specific DB contexts must not become normal runtime execution contexts.
- Expected behavior: runtime code injects canonical `IDbContextFactory<AppDbContext>`; maintenance code names the exceptional profile-specific factory explicitly.
- Disallowed shallow implementation: keeping a switchable factory name that hides whether code is runtime or maintenance.
- Passing proof: `bundle://proof/SB04/transcripts/profile-context-source-audit.txt`.
- Changed source files and hashes: `bundle://proof/SB08/transcripts/changed-file-hashes.txt`.
- Production assertions: `repo://src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs` defines `IProfileAppDbContextFactory`.
- Red-team negative case: source audit verifies process/automation runtime loops use canonical `IDbContextFactory<AppDbContext>`.
- Downstream dependency check: SB05/SB06 throughput and claim work build on canonical runtime context boundaries.
