# SB04 downstream invalidation — Release revalidation

Date: 2026-08-25  
Result: `PASS`

## Trigger

SB07 repairs changed two SB04-owned relay semantics: Responses requests now canonicalize omitted
`store` to JSON `false`, and persisted operation/model capability mismatches are rejected before
dispatch. Those changes named the SB04 wire-contract and adapter/capability invalidation keys and
could affect SB06 consumers of the shared-provider runtime path.

The earlier SB06 test transcripts resolved to Debug assemblies, so they were retained only as
chronology and were not accepted as current Release authority.

## Boundary review

The reopen delta changes relay policy and application dispatch behavior and adjusts the existing
`WorkspaceService` save path in its partial class so first-save structured-output defaults cannot
widen an existing or unsupported profile. It adds no project reference, public member/type family,
partial declaration, runtime provider kind, reflection bridge, or alternate execution path. Workspace
remains the canonical graph owner, the AgentFramework module remains the outer projection boundary,
and inner Providers/MAF code remains connector-neutral.

The current Unit and Integration Release assemblies were rebuilt successfully during the SB04
revalidation before these tests ran; the authoritative build transcripts are
`../../../SB04-openai-compatible-relay-streaming-images/proof/transcripts/sb04-reopen-build-unit-release-final.txt`
and
`../../../SB04-openai-compatible-relay-streaming-images/proof/transcripts/sb04-reopen-build-integration-release-final.txt`.
The SB04 project-reference audit is
`../../../SB04-openai-compatible-relay-streaming-images/proof/transcripts/sb04-reopen-project-references.txt`.
No product or test source changed between those clean builds and this downstream revalidation.

## Frozen Release proof

| Topic | Configuration | Discovered | Passed | Failed | Skipped | Evidence |
| --- | --- | ---: | ---: | ---: | ---: | --- |
| `SharedProviderRuntimeProfileMaterializerTests` | Release | 18 | 18 | 0 | 0 | `../transcripts/sb06-revalidate-list-materializer-release.txt`; `../transcripts/sb06-revalidate-run-materializer-release.txt` |
| `SharedProviderRuntimeProjectionIntegrationTests` | Release | 16 | 16 | 0 | 0 | `../transcripts/sb06-revalidate-list-runtime-projection-release.txt`; `../transcripts/sb06-revalidate-run-runtime-projection-release.txt` |
| `SharedProviderHybridSelectionTests` | Release | 10 | 10 | 0 | 0 | `../transcripts/sb06-revalidate-list-hybrid-release.txt`; `../transcripts/sb06-revalidate-run-hybrid-release.txt` |

All commands used `-c Release --no-build --no-restore -m:1`, listed the exact filtered tests
before execution, matched the frozen 18/16/10 counts, and passed without widening to an unfiltered
or broad lane.

## Decision

The SB04 semantic repair invalidated prior SB06 authority; the genuine Release proof above restores
trust in SB06 materialization, effective runtime projection, explicit personal/shared selection,
and no-fallback behavior. `PASS_SB06` and CP-04 are restored.
SB07 may resume from its preserved evidence, subject to its separate Docker test-budget authority.
