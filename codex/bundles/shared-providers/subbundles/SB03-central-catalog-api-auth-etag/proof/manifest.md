# SB03 governed proof manifest

State: `PASS`.

## Baseline and scope

SB03 was executed on branch `providers-shared` from commit
`e46f81d5ee33627dccb548732725e1c37e980ab5`. The cumulative worktree already contained SB00
through SB02 changes, so this manifest assigns only the SB03 semantic delta.

The pre-product CodeAnalytics snapshot was `snap-20260824235022-a4b340a8` with 13 scoped
projects, 31 direct product references, and no project-level cycle. The force-refreshed after
snapshot was `snap-20260825012213-a17e36ed`, captured at `2026-08-25T01:22:13Z`, with 14
projects, 736 source documents, 35 modules, 4,954 dependency edges, 33 direct product
references, no project-level cycle, the unchanged two module cycles and one type cycle, and no
error finding.

SB03 owns central publication eligibility and mutation, sanitized catalog/routing projection,
the five-row production relay-support registry, catalog and OpenAI models GET surfaces,
catalog-read/invoke policy separation, conditional GET, safe error/OpenAPI behavior, and the
secret-reference lifecycle needed to keep publication fail closed.

Inference POST dispatch, source synchronization, imported runtime projection, sharing UI,
three-instance proof, and exported OpenAPI/SharedInfo skills remain downstream-owned.

## Requirement-to-evidence map

| Requirement group | Proven behavior | Portable evidence |
| --- | --- | --- |
| FR-001–FR-005, FR-009 | publication is explicit; stale or ineligible publication fails without observer/activity effects; unpublish removes discovery | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/behavior/publication-catalog-api.md` |
| FR-006–FR-008, FR-010 | catalog projection uses stable public identities, canonical revisions/ETags, bounded health, and contains no private provider fields | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/security/catalog-authorization-containment.md` |
| FR-011, FR-020, FR-021, FR-024 | OpenAI model projection uses opaque routing IDs, keeps duplicate upstream model names distinct, and preserves route-specific envelopes | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/behavior/publication-catalog-api.md` |
| NFR-001–NFR-003, NFR-008–NFR-012 | secret existence is checked without resolution; read and invoke scopes are separate; access context is not authentication; failures are redacted | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/security/catalog-authorization-containment.md` |
| NFR-015–NFR-022 | Abstractions is SDK-free, Http owns concrete support rows, Workspace owns policy/projection, Composition selects the implementation, and Web stays thin | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/architecture/changed-namespace-public-surface-review.md` |
| NFR-027, NFR-030, NFR-031 | canonical ETag/304, persisted-token-derived cache stamps, stable database identity, and current eligibility rechecks avoid process-local correctness | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/behavior/publication-catalog-api.md` |
| NFR-033, NFR-034, NFR-036, NFR-037 | deterministic focused selections discovered and passed exactly 18/14/10; no broad, paid-provider, browser, or multi-instance lane was run | this manifest and `bundle://subbundles/SB03-central-catalog-api-auth-etag/test-selection.json` |

The detailed invariant contract is
`bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/semantic-invariants.md`.

## Honest failing-first record

The exact 18-test unit class was authored before Workspace production behavior. Its first run
exited 1 because the policy, projection, cache, application service, and observer types were
absent. The first combined Web discovery attempt exited 1 on the genuine Workspace `CS9135`
compile defect; it is compile evidence, not a fabricated endpoint failure. The second discovery
attempt exposed the missing Http test reference. After discovery became clean, the catalog/API
selection discovered 14 and failed 14, and the authorization selection discovered 10 and failed
10 before their Web behavior existed. The complete record is in
`bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/chronology.md`.

## Production artifact matrix

| Artifact | Producer and consumer | Lifecycle and negative proof |
| --- | --- | --- |
| canonical publication metadata | Workspace, AgentFramework, and managed bootstrap paths call one typed writer; eligibility strictly reads the same constants and exact enum tokens | persisted profile changes rotate state; malformed, duplicated, numeric, incompatible, Azure/fallback/imported/Test, and pricing-only model claims fail closed |
| publication state | `SharedProviderPublicationApplicationService.ChangeAsync` mutates the stable publication row; query/projector consume only published eligible rows | expected-token check, commit, then cache observer/activity; stale, ineligible, or dangling-secret requests have no side effects |
| production relay registry | immutable Http catalog supplies exactly five connector/purpose rows through Abstractions; Composition registers it | scenario/process/import/fallback/audio/Azure and Test-classified rows are absent or rejected |
| catalog and routing index | projector emits sanitized DTOs and a private in-process routing index; query service and both GET surfaces consume the public snapshot | stable source/publication identity, opaque route IDs, persisted eligibility stamp, current recheck; unpublished/private/unknown targets remain absent |
| catalog ETag | canonical public representation produces the strong entity tag used by both GET surfaces | public changes rotate it, private changes do not; valid weak/strong/list/wildcard validators can return 304; malformed/mixed wildcard input returns a safe 400 |
| secret deletion boundary | Delete and both save paths acquire shared stable secret mutation keys; Workspace supplies the typed reference policy | deterministic save/delete interleaving blocks deletion and preserves valid references; exception text omits the secret GUID |
| authorization policies | typed catalog-read and invoke policies preserve the existing umbrella `api` convention | missing/malformed/expired/invoke-only credentials receive path-appropriate 401/403; access context never authenticates |

## Architecture evidence

- Before snapshot: `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/architecture/codeanalytics-before.md`.
- After snapshot: `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/architecture/codeanalytics-after.md`.
- Project-reference review: `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/architecture/project-references-after.md`.
- Public surface and partial-class review: `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/architecture/changed-namespace-public-surface-review.md`.
- Independent review: `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/architecture/independent-review.md`.

## Commands and durable evidence

| Gate | Result | Artifact |
| --- | --- | --- |
| Entry validator | pass | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-entry-validator.txt` |
| Pre-change worktree | captured | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-working-tree-before.txt` |
| Unit failing first | exit 1 on absent production types | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-failing-first-unit.txt` |
| Web discovery attempt 1 | exit 1 on Workspace `CS9135`; compile evidence only | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-failing-first-discovery.txt` |
| Web discovery attempt 2 | exit 1 on missing Http test reference; compile evidence only | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-test-discovery-before-web.txt` |
| Clean Web discovery | exit 0; planned 14/10 cases visible | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-final-test-discovery-before-web.txt` |
| Catalog/API failing first | 14 discovered, 0 passed, 14 failed | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-failing-first-catalog-api.txt` |
| Authorization failing first | 10 discovered, 0 passed, 10 failed | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-failing-first-authorization.txt` |
| Unit Release build | exit 0; 0 warnings, 0 errors | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-build-unit-release.txt` |
| Unit exact list/run | 18 discovered; 18 passed, 0 failed, 0 skipped | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-list-unit-release.txt`; `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-run-unit-release.txt` |
| Web Release build | exit 0; 0 warnings, 0 errors | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-build-web-release.txt` |
| Integration Release build | exit 0; 0 warnings, 0 errors | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-build-integration-release.txt` |
| Catalog/API exact list/run | 14 discovered; 14 passed, 0 failed, 0 skipped | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-list-catalog-api-release.txt`; `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-run-catalog-api-release.txt` |
| Authorization exact list/run | 10 discovered; 10 passed, 0 failed, 0 skipped | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-list-authorization-release.txt`; `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-run-authorization-release.txt` |
| CodeAnalytics after | snapshot `snap-20260825012213-a17e36ed`; no project cycle or error finding | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/architecture/codeanalytics-after.md` |
| Anti-stub audit | exit 0; 56 selected files passed | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-anti-stub-audit.txt` |
| Secret/content scan | exit 0; no credential-shaped value or forbidden public provider field | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/transcripts/sb03-secret-content-scan.txt` |
| Changed-file and proof hashes | centralized inventory | `bundle://subbundles/SB03-central-catalog-api-auth-etag/proof/hashes.sha256` |

## Progression decision

The SB03 evidence is complete and supports progression to SB04. The proof does not claim a broad
solution gate, live provider traffic, browser coverage, or multi-instance deployment coverage;
those remain explicitly assigned to later subbundles.
