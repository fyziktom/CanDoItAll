# SB03 publication and catalog behavior proof

State: `PASS`.

## Focused validation

| Selection | Discovery | Result | Evidence |
| --- | ---: | ---: | --- |
| `SharedProviderPublicationAndCatalogTests` | 18 | 18 passed, 0 failed, 0 skipped | `../transcripts/sb03-list-unit-release.txt`; `../transcripts/sb03-run-unit-release.txt` |
| `SharedProviderCatalogApiIntegrationTests` | 14 | 14 passed, 0 failed, 0 skipped | `../transcripts/sb03-list-catalog-api-release.txt`; `../transcripts/sb03-run-catalog-api-release.txt` |
| `SharedProviderAuthorizationIntegrationTests` | 10 | 10 passed, 0 failed, 0 skipped | `../transcripts/sb03-list-authorization-release.txt`; `../transcripts/sb03-run-authorization-release.txt` |

The owning Unit, Web, and Integration Release builds each completed with zero warnings and zero
errors. Their command lines, timestamps, durations, and exit codes are preserved in
`sb03-build-unit-release.txt`, `sb03-build-web-release.txt`, and
`sb03-build-integration-release.txt`.

## Publication eligibility matrix

| Candidate | Expected result | Proven decision |
| --- | --- | --- |
| enabled valid production OpenAI chat with current manifest/schema, existing required secret, canonical Chat/Responses metadata, declared models, and production chat relay | eligible | accepted; capability intersection advertises only supported features/models |
| production OpenAI image with ImageGeneration/Responses | eligible | accepted and advertises only image capability |
| production ComfyUI image with ImageGeneration/ChatCompletions | eligible | accepted |
| disabled or invalid profile/manifest/schema/base URL/timeout/name | ineligible | rejected before publication |
| malformed, duplicated, missing, or numeric enum metadata | ineligible | strict JSON and exact named-token parsing reject it |
| missing, empty, deleted, or dangling required secret | ineligible | existence is required without resolving secret content |
| scenario/process mock, reconciliation import, runtime fallback Ollama, Azure, Test-classified descriptor, or non-execution connector | ineligible | production provenance/classification checks fail closed |
| unsupported purpose, transport, model, or absent relay descriptor | ineligible | exact connector-purpose-transport intersection rejects it with a sanitized actionable reason |

The real Workspace save path, AgentFramework registry save, and managed/bootstrap path use the
same canonical metadata writer. The Workspace characterization reloads the persisted row, proves
an explicit empty `suggestedModels` array when no list was supplied, re-evaluates eligibility, and
publishes it. Pricing rows are not silently promoted into advertised model support.

## Explicit publication lifecycle

`SharedProviderPublicationApplicationService.ChangeAsync` checks the expected publication token
and current eligibility, changes the stable publication row, commits, and only then invokes cache
observers and writes metadata-only activity. The positive case proves publish followed by
unpublish while retaining the stable public identity. The negative case proves ineligible and
stale requests leave publication state unchanged and emit no observer or activity effect.

Secret deletion and provider save use stable mutation keys for both old and target secret IDs.
The deterministic interleaving holds Delete inside the reference policy, starts Save, proves Save
cannot bypass the scope, then releases Delete. Delete fails with the typed blocked exception,
Save succeeds, both required secret rows remain, and no dangling profile is created.

## Sanitized catalog and private routing

The projector consumes only published, currently eligible sources and emits explicit public DTOs.
Database-backed source identity and publication identity are stable across rebuilds. Public model
routing IDs are opaque and include publication identity plus a SHA-256 model fingerprint, so two
providers exposing the same upstream model name remain distinct. Raw upstream model IDs and the
exact profile target exist only in the in-process `SharedProviderRoutingTarget` index.

Canonical ordering makes projection deterministic. Private-only changes to URL, secret,
configuration, notes, or other non-public state leave the public revision and ETag unchanged.
Display/capability/model/bounded-health changes rotate the applicable publication and catalog
revisions. Raw health is mapped conservatively: unchecked is `Degraded`, exact checked `Healthy`
is `Available`, and every other checked status is `Unavailable`.

## Persisted-stamp cache behavior

Two independent cache instances derive their stamp from persisted source/publication/profile
tokens and current required-secret existence. The cross-host test warms both instances, deletes a
referenced secret without a local cache observer, and requires both hosts to re-evaluate the row
away and converge on the changed ETag. This proves correctness does not depend solely on an
in-process invalidation event.

The deliberate per-query eligibility-input reload is the fail-closed choice for this subbundle.
A later scale optimization may introduce a transactional database change stamp, but cannot replace
persisted revocation awareness with observer-only validity.

## HTTP catalog behavior

Both GET surfaces delegate to `ISharedProviderCatalogQueryService` and expose the same strong
catalog ETag. `If-None-Match` supports RFC 9110 weak comparison, tag lists, and a standalone
wildcard. A matching validator returns 304; a non-match returns the current representation;
malformed syntax and a wildcard mixed with another tag return route-specific 400 envelopes.

Successful and controlled-error responses carry private/no-cache policy and a server request ID.
The native route uses the CanDoItAll envelope; `/openai/v1/models` uses the OpenAI error shape.
OpenAPI metadata describes both operations, `If-None-Match`, opaque access context, response
ETag/cache/request-ID headers, and the owned response set. Authentication-scheme export remains
SB11-owned.

## Honest red-to-green evidence

The unit red run exited 1 because production types did not exist. Before endpoint implementation,
clean discovery exposed the planned 14 and 10 cases; their first runs failed 14/14 and 10/10.
Those artifacts remain beside the green transcripts and are interpreted in `../chronology.md`.

