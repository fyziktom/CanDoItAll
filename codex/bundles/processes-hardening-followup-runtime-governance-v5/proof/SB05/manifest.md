# SB05 Proof Manifest

## Status

Completed.

## Source Assertions

- repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactProjectionLineage.cs
- repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs
- repo://src/CanDoItAll.Modules.Processes/Persistence/Configurations/ProcessRuntimeEntityConfigurations.cs
- repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260525184500_ProcessRuntimeGovernanceV5.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB05 runtime governance artifact | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactProjectionLineage.cs | repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs and bundle://proof/SB05/transcripts/passing.txt | Verified by bundle://proof/SB05/transcripts/source-assertions.txt and dotnet test proof | Rejected by bundle://proof/SB05/transcripts/failing-first.txt |

## Semantic Invariant Contract

- bundle://proof/SB05/semantic-invariants.md

## Failing-First Or Red-Team Proof

- bundle://proof/SB05/transcripts/failing-first.txt

## Passing Proof

- bundle://proof/SB05/transcripts/passing.txt
- Test name: `CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.RecordArtifactAsync_SB05_INV_001_dedupes_by_projection_identity_hash_before_display_key`

## Anti-Stub Audit

- bundle://proof/SB05/transcripts/anti-stub-audit.txt

## Changed-File Hashes

- bundle://proof/SB05/transcripts/changed-file-hashes.txt
- `9851efe5465d906abe3d79948fbf935e01eae9313433191fd496a3ea4e9e88b3`  `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactProjectionLineage.cs`
- `55b75f3c802b11749d24305820afeb6a3b9e5444af643e37dd20ab83275b2f0a`  `repo://src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs`
- `3fffed059ebc5e6d14fb1ca8c1806bcac8418c919b4dcaadb0f8be7950221bd4`  `repo://src/CanDoItAll.Migrations.PostgreSql/Migrations/20260525184500_ProcessRuntimeGovernanceV5.cs`
- `3083277fccf897fb6a73f49bef706d3584fd0250164f245b831a0309f01fccc6`  `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

## Validation

- Focused proof commands passed for SB05; see bundle://proof/SB05/transcripts/passing.txt.
- Source assertions passed for SB05; see bundle://proof/SB05/transcripts/source-assertions.txt.
- Anti-stub audit found no stub-only production implementation; see bundle://proof/SB05/transcripts/anti-stub-audit.txt.

## Blockers

None.
