# SB03 Proof Manifest

## Subbundle

SB03-canonical-runtime-db-pooled-factory — Completed.

Owned requirements: R5, R6, R10.

Semantic invariant contract: `bundle://proof/SB03-canonical-runtime-db-pooled-factory/semantic-invariants.md`.

## Changed Files

| File | SHA-256 before | SHA-256 after | Reason |
|---|---|---|---|
| `repo://src/CanDoItAll.Infrastructure/ControlPlane/CanonicalRuntimeDatabase.cs` | New | See `bundle://proof/SB08-final-validation-benchmark-gate/changed-file-hashes.tsv` | Resolves canonical runtime profile once per process generation. |
| `repo://src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` | See hash inventory | See hash inventory | Registers pooled canonical `AppDbContext` factory. |
| `repo://src/CanDoItAll.Infrastructure/Persistence/SwitchableAppDbContextFactory.cs` | See hash inventory | See hash inventory | Delegates normal contexts to the pooled factory while retaining profile-specific admin contexts. |
| `repo://src/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs` | See hash inventory | See hash inventory | Removes runtime lease constructor/dispose hot path. |
| `repo://tests/CanDoItAll.Tests.Unit/DatabaseRuntimeSwitchingTests.cs` | See hash inventory | See hash inventory | Updates runtime switching expectations for restart-first canonical runtime. |
| `repo://tests/CanDoItAll.Tests.Integration/DatabaseRuntimeSwitchingIntegrationTests.cs` | See hash inventory | See hash inventory | Proves restart boundary and canonical runtime identity. |

## Commands

| Command | Transcript path | Result |
|---|---|---|
| Final build | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-build-final.txt` | Passed. |
| Full unit tests | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-unit-full.txt` | Passed 788 tests. |
| Focused PostgreSQL integration sweep | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` | Passed 452 tests. |
| Source assertions | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt` | Passed. |
| Residue/bottleneck audit | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/audit-residue-and-bottlenecks.txt` | Passed. |

## Semantic Positive Proof

Normal runtime contexts are created from `AddPooledDbContextFactory<AppDbContext>` using `ICanonicalRuntimeDatabase.Profile`. The canonical profile is resolved at startup and normal context creation no longer asks the profile resolver or acquires a switch lease per context.

## Adversarial Negative Proof

The profile-specific creation path remains explicit through `CreateDbContextForProfileAsync`; admin/transfer flows can open selected profiles without mutating the canonical runtime. This rejects the shallow implementation that would use one pooled factory for every profile and break Data Sources maintenance tooling.

## Canonicality Proof

`CanonicalRuntimeDatabase` owns runtime identity for the process generation. `SwitchableAppDbContextFactory.CreateDbContext*` uses the pooled canonical factory; only profile-specific admin methods build per-profile options.

## Anti-Stub Audit

`bundle://proof/SB08-final-validation-benchmark-gate/transcripts/anti-stub-audit.txt` found no stub markers in changed production files.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| Canonical runtime profile generation | `repo://src/CanDoItAll.Infrastructure/ControlPlane/CanonicalRuntimeDatabase.cs` | `repo://src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/source-assertions.txt` | `bundle://proof/SB08-final-validation-benchmark-gate/transcripts/dotnet-test-integration-focused.txt` |

## Browser Validation Analytics

N/A. SB03 has no direct UI behavior.

## Remaining Risks

No code risk remains for the canonical factory path. A future hot-switch maintenance mode must not route normal context creation back through per-context resolution.
