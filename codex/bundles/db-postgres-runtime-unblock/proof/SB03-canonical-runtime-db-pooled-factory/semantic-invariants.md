# SB03 Semantic Invariants

## Invariants

### SB03-I1 Normal runtime DbContexts use one canonical profile per process generation

Raw note: "A running process has exactly one canonical runtime database profile per generation."

Expected behavior: `ICanonicalRuntimeDatabase` resolves the active PostgreSQL profile once during startup and the normal `IDbContextFactory<AppDbContext>` uses that profile through the pooled factory.

Shallow-pass trap: cache a string connection value ad hoc while still resolving active profile or acquiring switch leases per context.

Adversarial negative proof: focused integration tests exercise runtime switch behavior and prove activation does not mutate the current running profile in-process.

Semantic positive proof: `repo://src/CanDoItAll.Infrastructure/ControlPlane/CanonicalRuntimeDatabase.cs`, `repo://src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`, and `repo://src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs`.

Production assertions: `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt`.

Changed source files: see `bundle://proof/SB08-final-validation-benchmark-gate/changed-file-hashes.tsv`.

Downstream dependency check: SB04 restart-first activation depends on this canonical runtime identity.

### SB03-I2 Admin profile contexts remain explicit and separated

Raw note: "Profile-specific context creation still works for Data Sources admin actions."

Expected behavior: transfer/schema/admin operations call the profile-specific factory path; normal app work uses the pooled canonical factory.

Shallow-pass trap: route every context through the pooled canonical factory and break transfer/schema validation for non-active PostgreSQL profiles.

Adversarial negative proof: Data Sources component and Playwright tests exercise activation/preview behavior after the pooled factory change.

Semantic positive proof: `repo://src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs` retains `CreateDbContextForProfileAsync`.

Production assertions: `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Canonical runtime profile | `repo://src/CanDoItAll.Infrastructure/ControlPlane/CanonicalRuntimeDatabase.cs` | `repo://src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-build-final.txt` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` |
