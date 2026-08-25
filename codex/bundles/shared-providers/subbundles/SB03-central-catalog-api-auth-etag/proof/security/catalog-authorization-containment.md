# SB03 catalog authorization and containment proof

State: `PASS`.

## Secret and private-data containment

The catalog is an explicit sanitized contract; no EF or AgentFramework provider profile is
serialized. Public catalog/model output excludes internal profile IDs, upstream base URLs,
secret IDs, secret names or values, configuration JSON, private notes, and raw health text.
The routing identifier is opaque, while the exact upstream model and profile target remain in the
private in-process routing index.

The focused scan completed with exit 0 and found no credential-shaped value and no forbidden
private-provider field in public catalog contracts or endpoint response types:
`../transcripts/sb03-secret-content-scan.txt`.

Eligibility and query code check only whether the referenced required `SecretRecord` exists; they
do not resolve its value. The publication cache stores no secret material. Secret deletion errors
omit the target GUID from their message while retaining a typed identifier for trusted callers.

## Authorization matrix

| Credential/scope | Native catalog | OpenAI models | Security result |
| --- | --- | --- | --- |
| granular catalog-read | allow | allow | least-privilege discovery |
| existing umbrella `api` | allow | allow | backward-compatible convention |
| invoke only | 403 native envelope | 403 OpenAI envelope | relay permission does not imply discovery permission |
| missing bearer token | 401 native envelope | 401 OpenAI envelope | no anonymous discovery when API auth is enabled |
| malformed or expired bearer token | 401 native envelope | 401 OpenAI envelope | invalid credentials never degrade to anonymous |

The exact authorization selection discovered 10 and passed 10/10; see
`../transcripts/sb03-list-authorization-release.txt` and
`../transcripts/sb03-run-authorization-release.txt`. Catalog-read and invoke use separate typed
scope constants and policies. When API authorization is globally disabled, the existing optional
auth convention leaves catalog routes anonymous; SB03 does not invent another authentication
mode.

The access-context reference is parsed as opaque request context. It neither authenticates the
caller nor changes scope evaluation. Malformed values return a safe path-specific 400.

## Error, log, and cache controls

- Both surfaces set private/no-cache headers and a server-generated request ID.
- Controlled application exceptions become stable 503 responses without reflecting internal
  exception messages; validation failures use bounded client-safe details.
- Native and OpenAI paths retain their distinct envelopes for 400, 401, 403, and 503 responses.
- Logs contain controlled request metadata and do not include response bodies, credentials,
  resolved secrets, provider configuration, or raw exception content.
- `If-None-Match` uses typed parsing and rejects malformed or mixed-wildcard input instead of
  turning invalid input into a cache hit.
- Public revisions hash only normalized sanitized output; private field changes cannot leak
  through ETag churn.

The exact catalog/API selection discovered 14 and passed 14/14; see
`../transcripts/sb03-list-catalog-api-release.txt` and
`../transcripts/sb03-run-catalog-api-release.txt`.

## Fail-closed secret lifecycle

Required secret IDs must be non-empty and reference an existing row during save, publish, and
catalog re-evaluation. `SecretService.DeleteAsync`, Workspace save, and AgentFramework registry
save share deterministic multi-key mutation scope keys. Deletion consults required typed reference
policies before commit. The deterministic concurrency Fact proves that a save cannot race a
deletion into a dangling provider reference, and direct secret removal is re-evaluated out of
both warmed catalog caches.

## Structural controls

The anti-stub validator completed with exit 0 for 56 selected production and test files:
`../transcripts/sb03-anti-stub-audit.txt`. The after CodeAnalytics snapshot reports no project
cycle and no error finding. Http descriptors are production-only and actual-host composition
contains exactly five rows; scenario/process/import/fallback/audio/Azure rows are absent.

## Residual ownership

SB03 does not claim live-provider network containment, inference POST authorization, exported
OpenAPI security-scheme coverage, or deployed three-instance/database behavior. SB04, SB07,
SB11, and SB12 own those gates respectively.

