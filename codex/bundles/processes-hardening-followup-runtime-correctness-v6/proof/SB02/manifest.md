# SB02 Proof Manifest

## Status

Completed.

## Source Assertions

- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs`: `RecordArtifactAsync` normalizes projection lineage once, reads `ProjectionIdentityHash` from that normalized object, dedupes by hash before `ExternalReferenceKey`, and persists both hash and lineage JSON from the normalized object.
- `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactProjectionLineage.cs`: `SerializeNormalized` serializes the already-normalized lineage without recomputing identity.
- `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`: integration tests prove different display keys and long bounded display keys dedupe by projection identity hash.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| SB02 verified runtime behavior | repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs | bundle://proof/SB02/manifest.md | bundle://proof/SB02/transcripts/passing.txt | bundle://proof/SB02/transcripts/failing-first.txt |
## Semantic Invariant Contract

- `bundle://proof/SB02/semantic-invariants.md`

## Failing-First or Red-Team Proof

- Transcript: `bundle://proof/SB02/transcripts/failing-first.txt`

## Passing Proof

- Transcript: `bundle://proof/SB02/transcripts/passing.txt`
- Test name: `CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.RecordArtifactAsync_SB05_INV_001_dedupes_by_projection_identity_hash_before_display_key`
- Test name: `CanDoItAll.Tests.Integration.ProcessesServiceIntegrationTests.RecordArtifactAsync_SB02_INV_001_dedupes_long_display_keys_by_projection_identity_hash`

## Anti-Stub Audit

- Transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`

## Changed-File Hashes

- Transcript: `bundle://proof/SB02/transcripts/changed-file-hashes.txt`
- `E04075DFD91323B1BDEB35E6CC739226F53A21635D9165A172652805B01D5DFC` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessArtifactProjectionLineage.cs`
- `8C6DEF0E9E5C9DF8C2D51175C73F0790E98C5FF24151665FA8ACD3F2B0D980DE` `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.Operations.cs`
- `E91A83793F168554108584E40E55BF9EDC142977DC9214349C975EE7AC9D449E` `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`

## Validation

- Focused integration tests passed: `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~RecordArtifactAsync_SB05_INV_001_dedupes_by_projection_identity_hash_before_display_key|FullyQualifiedName~RecordArtifactAsync_SB02_INV_001_dedupes_long_display_keys_by_projection_identity_hash"`.

## Blockers

None.


