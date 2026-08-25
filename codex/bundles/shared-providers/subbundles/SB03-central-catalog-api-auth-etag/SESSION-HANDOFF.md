# SB03 session handoff

State: `COMPLETE`

## Outcome

SB03 passed its central-publication/catalog/API architecture gate. Workspace now owns explicit
publication eligibility, administrator publish/unpublish, canonical sanitized catalog/routing
projection, and persisted cache invalidation. Web exposes authenticated native catalog and
OpenAI-compatible models discovery with strict conditional requests and path-correct errors.
The central half of CP-03 is complete as `PASS_SB03`; CP-03 remains `OPEN` because SB04 owns the
relay half. Only SB04 may proceed.

## Current repository state

- branch: `providers-shared`
- commit before: `e46f81d5ee33627dccb548732725e1c37e980ab5`
- commit after: `e46f81d5ee33627dccb548732725e1c37e980ab5` (no commit created)
- working tree before: completed SB00-SB02 plus SB03 readiness/failing-first evidence, captured at
  SB03 entry
- working tree after: uncommitted SB00-SB03 source, tests, and Governed proof; see
  `proof/transcripts/sb03-working-tree-final.txt`
- unrelated changes preserved: no pre-existing unrelated change was staged, committed, discarded,
  or overwritten

## Changed files

- SharedProviders Abstractions gained validated OpenAI discovery envelopes and typed relay-support
  descriptors without an SDK/Web/EF dependency;
- a cohesive `CanDoItAll.SharedProviders.Http` descriptor-only project defines the five production
  connector/purpose rows and is registered only by outer Composition;
- Workspace gained eligibility, explicit publication, canonical projection/routing, query/cache,
  persisted invalidation, and API-scope ownership in cohesive SharedProviders files;
- Web gained thin native/OpenAI GET endpoints, catalog/invoke policies, safe native/OpenAI error
  writing, RFC 9110 conditional handling, request/access-context headers, and marker-scoped OpenAPI
  metadata;
- Unit and Integration gained exactly the three focused SB03 classes plus narrowly required host,
  project-reference, and composition wiring changes.

The complete inventory is `proof/changed-files.md`; after-state hashes are
`proof/hashes.sha256`.

## Architecture evidence

- decision: `PASS_SB03` for the central half; shared CP-03 remains `OPEN` for SB04 relay proof
- ProjectReference before: `proof/architecture/project-references-before.md`
- ProjectReference after: `proof/architecture/project-references-after.md`
- CodeAnalytics before: `snap-20260824235022-a4b340a8`
- CodeAnalytics after: `snap-20260825012213-a17e36ed`
- graph: 13 to 14 scoped projects, 31 to 33 direct product references, and zero project cycles;
  the only new edges are `SharedProviders.Http -> SharedProviders.Abstractions` and outer
  `Composition -> SharedProviders.Http`
- boundaries: Workspace retains only its Abstractions dependency, Web performs discovery only,
  and Abstractions remains an SDK/Web/EF-free leaf contract assembly
- public/partial review: public contracts remain in Abstractions/Workspace or the descriptor
  integration boundary; Web implementation stays internal; no partial type was added or extended
- independent review: `PASS` after strengthening expired-token, controlled-failure,
  production-descriptor, RFC mixed-wildcard, and OpenAPI header-schema vectors

## Build and focused test evidence

| Topic | Expected | Actual | Passed | Failed | Skipped | Artifact |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| `SharedProviderPublicationAndCatalogTests` | 18 | 18 | 18 | 0 | 0 | `proof/transcripts/sb03-run-unit-release.txt` |
| `SharedProviderCatalogApiIntegrationTests` | 14 | 14 | 14 | 0 | 0 | `proof/transcripts/sb03-run-catalog-api-release.txt` |
| `SharedProviderAuthorizationIntegrationTests` | 10 | 10 | 10 | 0 | 0 | `proof/transcripts/sb03-run-authorization-release.txt` |

Unit, Web, and Integration Release builds report zero warnings/errors. Exact discovery is recorded
before every run. The final 18/14/10 sources were authored before Web behavior and have honest
failing-first transcripts; later independent-review vectors strengthened the same fixed test
cardinalities without rewriting that chronology.

## Positive behavior

- Eligibility intersects enabled/valid production profile metadata with the registered relay
  support descriptor; explicit publish/unpublish commits activity and invalidation only after a
  successful concurrency-checked mutation.
- Projection includes only explicit eligible publications, preserves stable public identities,
  produces distinct routing IDs for duplicate upstream model names, and deterministically derives
  canonical public revisions and a strong catalog ETag.
- Persisted invalidation stamps keep cache behavior correct across query-service instances and
  recheck current eligibility.
- `GET /api/shared-providers/v1/catalog` and
  `GET /api/shared-providers/openai/v1/models` return native and OpenAI-compatible public shapes,
  accept the catalog-read or umbrella scope, and describe request/response headers in OpenAPI.
- RFC 9110 strong/weak/list/multiple-header/wildcard validators use weak GET comparison and return
  304 when any valid validator matches.

## Negative behavior

- Disabled, malformed, synthetic, imported, fallback, unknown, non-execution, and unsupported
  profiles cannot be published or projected; the production descriptor catalog excludes Azure,
  audio, scenario/process, imported, and fallback profiles.
- Unknown routing IDs, ineligible/stale publication attempts, invalid access context, truly
  malformed or bounded-overflow entity-tag lists, and mixed wildcard/tag lists fail explicitly.
- Missing, malformed, expired, and wrong-scope tokens produce 401/403 with the correct native or
  OpenAI envelope. Invoke-only scope cannot read the catalog; the umbrella scope remains compatible.
- Controlled query failures return bounded 503 envelopes without reflecting exception, connection,
  endpoint, or secret sentinel content.

## Security and redaction

Catalog projection is allowlisted and serializes no internal profile ID, connector implementation,
private URI, secret ID/name/value, configuration JSON, note, raw health error, or exception detail.
Failure logging records only the exception type and server trace identifier. All catalog responses,
including errors and 304, carry server request ID and `private, no-cache`; ETag appears only when a
current representation is available. Access context remains bounded opaque metadata and cannot
satisfy authentication or either API scope.

## Remaining risks and downstream constraints

- SB04 owns inference POST routes, real adapters, no-open-proxy enforcement, streaming, tools,
  structured output, vision/images, cancellation, upstream error mapping, and invocation-audit
  population. CP-03 cannot close before that proof passes.
- SB05 owns source networking, SSRF/DNS/redirect/TLS policy, conditional sync, and client selection.
- SB06/SB07 own imported runtime projection, no-fallback behavior, and real multi-instance proof.
- SB11 owns final security-scheme/OpenAPI export and SharedInfo synchronization.
- SB12 retains the single broad aggregate and final running-stack closure.

These are assigned downstream constraints, not missing SB03 central proof.

## Reopen triggers observed

None. Reopen SB03 if catalog wire shape or canonical serialization changes, the production support
descriptor matrix changes, publication/profile invalidation ownership changes, optional API auth or
umbrella-scope convention changes, or SB04 requires a discovery contract change.

## Progression decision

- result: `PASS`
- next subbundle: `SB04`
- reason: eligibility/publication/cache architecture, sanitized catalog/models routes,
  authorization, RFC 9110 conditional behavior, OpenAPI metadata, exact focused tests, graph/public
  surface review, and negative/security proof pass; relay behavior remains explicitly SB04-owned
