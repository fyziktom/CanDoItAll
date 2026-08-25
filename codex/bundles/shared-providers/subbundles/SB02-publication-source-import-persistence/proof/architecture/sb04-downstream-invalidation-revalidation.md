# SB04 downstream invalidation and SB02 restored trust

State: `RESTORED`
Recorded: `2026-08-25`

## Purpose and chronology

SB02 originally passed CP-02 with exact 18/14/6 focused selections. That result and its original
transcripts remain historical evidence; they have not been replaced or rewritten.

SB04 subsequently changed the SB02-owned invocation persistence and usage contracts. Those edits
temporarily invalidated trust in the affected SB02 state, PostgreSQL mapping, migration, and
reference-safety evidence even though SB02 itself remained historically complete. Trust was
restored only after rebuilding the affected assemblies and executing the fresh evidence below.

## Invalidation triggers

The downstream changes that required revalidation were:

- adding the strongly typed relay operation and metadata-only `ImageCount` to invocation state;
- making token and image usage mutually exclusive by operation across transition validation and
  the PostgreSQL check constraint;
- amending the existing migration, its designer, and the model snapshot to carry the same
  operation-aware constraint;
- preserving the existing eight-parameter `SharedProviderInvocationCompletion` constructor and
  deconstruction ABI while adding `ImageCount` as an init-only property;
- updating SB02 state and persistence tests to prove the additive metadata field, operation
  invariants, round-trip persistence, and database rejection of invalid cross-operation usage;
- changing the Unit and Integration assemblies used by the frozen SB02 selections. The deletion
  test source itself did not change, but its production composition and shared schema did.

## Affected-file hash comparison

The SB02-close hashes come from `proof/changed-files.md`. Current hashes were calculated from the
post-SB04 files with SHA-256. This table is an invalidation inventory, not a rewrite of the
original changed-file manifest.

| File | SB02-close SHA-256 | Post-SB04 SHA-256 |
| --- | --- | --- |
| `src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260824224847_AddSharedProviderPersistence.cs` | `0fb54b90df0c4ef8b8cf1c4be5d8cd7635a738f9747b0ace00fc37f6ea0898e7` | `cbd1e533d11c535c49e4fd4e9956346a976d1643f822f42d36b4b6cb318a1f18` |
| `src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/20260824224847_AddSharedProviderPersistence.Designer.cs` | `346ee69204f82997deca551de301ea97b081ba302884143300411d173564729f` | `282bca98a6fd88251959fc920cfd9fc144f3f13b0092b1f39c2277da1f67a638` |
| `src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/AppDbContextModelSnapshot.cs` | `86836607cedd6818324c9b0d1857961322f5607eae298059477607ab736eae44` | `c49fe1a976dec36ee29c53142803362e932eccfc01d71952acab8766b3a8d275` |
| `src/MAF/Common/CanDoItAll.AgentFramework.Usage/ProviderUsageContracts.cs` | `125d03290294848fe288628b3201a3829ae6cbdc15767dedf458729d04615e92` | `b8707f5175a787bb5a8c24968f44bc358d4a286e75694f239a55715e66297d8f` |
| `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderStates.cs` | `8c1eff69eaa4443e4a7bbe8416d584bf7b0f0a49cc379879b9773503bd5bc366` | `c5bb2c36f558dda17304fa9f2094c861711a4a54ae4d9592ec93aa01fa6817ef` |
| `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderInvocationRecord.cs` | `2f8062f49da3d98f716d081b95e5f240ea0f16d0f57d069bd2b4cf7b055df06b` | `058cb8eeadde866bdc3273b3224a841b161b807d22c99f10e7240a073224d684` |
| `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderInvocationRecordConfiguration.cs` | `ba54d972c923407dfacb2f30bb46e0e0d531fe1cd8c9e67edeb744ed7d29fd3e` | `ef3859e7fbdd3a612ddf8e42bdd5ac31aeb91aa61126026cf2192d8515ea727b` |
| `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderInvocationTransitions.cs` | `e9f4f1c20678576638c5ca07f7c0d84087601ccc59a9ff4a456322f6d6f87a53` | `e6545c145440442523798c75f65b096f327f7fc0ee530c9c04bfda7ddbd2f05a` |
| `src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderInvocationAuditService.cs` | `02d09088eeda1cb1349a7f6052ae5628d2e3fe362ac32efb5128cf26ed518020` | `ce919c90ff9f58fc4179751517d62bb93a4dfaf865adaa89f125012d35560062` |
| `tests/Unit/CanDoItAll.Tests.Unit/SharedProviderStateModelTests.cs` | `0efa7fdb80c0b85875fdf0eb602c5c37c764e987ede339e2ac75df3a11679b6f` | `cd4242b81b35f9d7fc708e618fbb2b7d0bfdc1e09a5c5647e9658c7eb6a8b1ab` |
| `tests/Integration/CanDoItAll.Tests.Integration/SharedProviderPersistenceIntegrationTests.cs` | `d1c2d9ba643b18c4a50d5d254a226942523081704b171f2bc1576d2974b4ca0a` | `ecb0f72bb8865e516234cbfa25799082a51ea59a30b1824a9c8abff5c3eb6ff3` |
| `tests/Integration/CanDoItAll.Tests.Integration/SharedProviderDeletionReferenceIntegrationTests.cs` | `5f8764773bf47c80e9a0fbab743301ae78598015d3c053ddcefbbdaf4dce0b92` | `5f8764773bf47c80e9a0fbab743301ae78598015d3c053ddcefbbdaf4dce0b92` |

SB04 also added or changed downstream relay finalization, recovery, and usage-projection files
that were not SB02-owned changed files, so SB02 has no close-time hash for those paths. Their
complete inventory belongs to SB04 proof.

## Fresh exact evidence

All list and run commands retained the frozen SB02 filters and used Release binaries with
`--no-build --no-restore` and `/m:1` after the affected assemblies had been rebuilt.

| Selection or gate | Discovery | Result | Evidence |
| --- | ---: | --- | --- |
| `SharedProviderStateModelTests` list | 18 | exit 0 | [`sb02-list-state-release-after-sb04.txt`](../transcripts/sb02-list-state-release-after-sb04.txt) |
| `SharedProviderStateModelTests` run | 18 | 18 passed, 0 failed, 0 skipped; 3.087 s | [`sb02-run-state-release-after-sb04.txt`](../transcripts/sb02-run-state-release-after-sb04.txt) |
| `SharedProviderPersistenceIntegrationTests` list | 14 | exit 0 | [`sb02-list-persistence-release-after-sb04.txt`](../transcripts/sb02-list-persistence-release-after-sb04.txt) |
| `SharedProviderPersistenceIntegrationTests` run | 14 | 14 passed, 0 failed, 0 skipped; 41.120 s | [`sb02-run-persistence-release-after-sb04.txt`](../transcripts/sb02-run-persistence-release-after-sb04.txt) |
| `SharedProviderDeletionReferenceIntegrationTests` list | 6 | exit 0 | [`sb02-list-deletion-release-after-sb04.txt`](../transcripts/sb02-list-deletion-release-after-sb04.txt) |
| `SharedProviderDeletionReferenceIntegrationTests` approved run | 6 | 6 passed, 0 failed, 0 skipped; 30.368 s | [`sb02-run-deletion-release-after-sb04-approved.txt`](../transcripts/sb02-run-deletion-release-after-sb04-approved.txt) |
| EF pending-model gate | n/a | exit 0; no pending model changes | [`sb02-ef-pending-model-release-after-sb04.txt`](../transcripts/sb02-ef-pending-model-release-after-sb04.txt) |

The first deletion run in the restricted sandbox is retained at
[`sb02-run-deletion-release-after-sb04.txt`](../transcripts/sb02-run-deletion-release-after-sb04.txt).
It exited 1 with 0/6 passing because every test failed during application bootstrap with
`UnauthorizedAccessException` for the user-local control-plane lock file. No deletion assertion
ran. The approved rerun used the identical test command with the required filesystem access and
passed 6/6. The first run is therefore environment-failure chronology, not evidence of a product
regression and not the passing result.

The EF transcript also reports the installed tools/runtime patch-version warning (`10.0.3` tools,
`10.0.4` runtime). The command still exited 0 and explicitly reported no pending model changes.

## Migration amendment assumption

The in-place amendment of `20260824224847_AddSharedProviderPersistence` is valid only while that
migration has never been applied to a durable or otherwise non-disposable database. That is the
bundle execution assumption for this uncommitted migration. If any durable database has applied
the earlier migration body, rewriting it is invalid: retain the applied migration unchanged,
create a forward migration for the operation and image-usage columns/constraint, and repeat both
clean-database and upgrade-path PostgreSQL proof.

## Restored-trust conclusion

Result: `PASS — TRUST RESTORED`.

The exact 18/14/6 selections pass after the SB04 schema and ABI changes, database-backed usage
constraints are exercised by the 14-test persistence lane, production reference safety remains
green in the approved 6-test run, and EF reports migration/model alignment. SB02's original PASS
history remains intact; this additive overlay closes only the named downstream invalidation.
