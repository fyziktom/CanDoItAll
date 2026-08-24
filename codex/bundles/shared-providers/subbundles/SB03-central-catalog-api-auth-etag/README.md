# SB03 — Central publication policy, sanitized catalog, authorization, and ETag

State: `LOCKED`  
Proof tier: `Governed`  
Depends on: `SB02`  
Next on pass: `SB04`

## Objective

Implement central publication eligibility/application services and the native catalog/models discovery APIs with strict sanitization, scopes, public representation hashes, and cache invalidation.

## Observable outcome

An authenticated client can discover only explicitly published and actually supported provider/model routes without learning central credentials or private configuration.

## Inputs and current-state anchors

- Bundle root execution contract and architecture documents.
- Current repository state, not only the prepared SHA.
- Relevant source/test impact maps.
- Completed proof and handoff from every dependency.
- Current mandatory SharedInfo skills.

## Scope

- Implement publication eligibility as capability intersection over provider profile, connector manifest, registered relay support descriptor, purpose/model, and production/test classification.
- Implement local publish/unpublish application service with concurrency and activity/audit entry.
- Implement sanitized catalog projector and canonical JSON/hash/revision/ETag.
- Implement catalog routing index/model list projection.
- Add catalog-read and invoke scope constants/policies; catalog uses catalog-read.
- Map GET /api/shared-providers/v1/catalog.
- Map GET /api/shared-providers/openai/v1/models with OpenAI list envelope.
- Handle If-None-Match/304 and cache invalidation from provider/publication commits.
- Use native CanDoItAll errors for catalog and OpenAI error envelope for models route failures where required.
- Add request ID and no-cache/private headers.
- Add OpenAPI metadata for these routes.
- Add service/API/auth/redaction/ETag/composition tests.

## Out of scope

- No inference POST yet.
- No client source HTTP/sync.
- No UI publish action.
- No Docker multi-instance lane.

## Implementation sequence

1. Keep endpoint file thin and delegate to Workspace application service.
2. Map public DTOs explicitly; never serialize EF or AgentFramework profiles.
3. Compute public hash from canonical sanitized representation only.
4. Exclude scenario/process mocks and fallback profiles in production.
5. Reject publish if no relay adapter supports the profile's exact purpose/transport/capabilities.
6. Do not expose raw health errors; map bounded public availability.
7. Ensure duplicate model names create distinct routing IDs.
8. Wire policies through existing optional JWT convention and umbrella scope behavior.
9. Add cache invalidation observer without storing secrets in cache.

## C# Architecture Impact

This subbundle is architecture-significant. Re-read
`architecture/00-csharp-current-state-inventory.md` through
`architecture/04-csharp-testability-plan.md`, update the affected checkpoint, and stop rather
than use a boundary workaround.

## Boundary Ownership

Workspace owns eligibility/public projection. Web owns routes/auth/envelopes. SharedProviders Abstractions owns DTOs. Http implementation may expose support descriptors through a registered abstraction but no dispatch yet.

## Dependency Direction

Web/Composition may reference Http implementation for adapter descriptors; Workspace receives descriptor catalog abstraction. Workspace does not reference concrete Http.

Record before and after `ProjectReference`/namespace direction even when no reference is
expected to change. A no-change result is still evidence.

## Pattern Decision

Policy service, explicit projection, canonical representation hash, query endpoint.

Do not introduce an adjacent alternative pattern without reopening the owning ADR and
recording why the selected pattern failed.

## Testability Contract

Pure projector/policy tests plus real Web host authorization/ETag/OpenAPI tests.

Every new behavior needs one realistic positive proof and one meaningful negative proof. Test
existence, file counts, status codes alone, or mocked self-assertions do not prove behavior.

## Partial Class Policy

New endpoint and application service files. Do not add catalog logic to ApiEndpointRouteBuilderExtensions beyond one map call.

A large partial or monolithic file is a gate failure unless the architecture review documents
a narrow unavoidable reason.

## Architecture Proof Required

- Publication eligibility matrix.
- Sanitized JSON snapshot and forbidden-field scan.
- ETag deterministic/change/no-change vectors.
- Auth scope matrix.
- Models/catalog route integration and OpenAPI presence.
- Composition registry excludes production mocks.

## Test selection

| Topic | Owning project/lane | Stable filter | Planned expected discovery | Selection reason |
| --- | --- | --- | ---: | --- |
| `SharedProviderPublicationAndCatalogTests` | `tests/Solutions/CanDoItAll.Tests.Unit.slnx` | `FullyQualifiedName~SharedProviderPublicationAndCatalogTests` | 18 | Covers policy intersection, projection, hash and redaction. |
| `SharedProviderCatalogApiIntegrationTests` | `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | `FullyQualifiedName~SharedProviderCatalogApiIntegrationTests` | 14 | Covers HTTP catalog/models, ETag and error envelopes. |
| `SharedProviderAuthorizationIntegrationTests` | `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | `FullyQualifiedName~SharedProviderAuthorizationIntegrationTests` | 10 | Covers umbrella/granular/missing/invalid token behavior. |

Before running a test topic:

1. build the owning production/test assembly;
2. run `--list-tests` when it is a .NET test lane;
3. compare actual discovery with the planned count;
4. update the planned count only before execution and with a written implementation-based
   reason;
5. reject zero discovery;
6. record transcript and counts in `proof/proof-manifest.json`.

Do not run an unfiltered project or broader lane unless this subbundle explicitly owns it.

## Acceptance criteria

- Unpublished profile is absent.
- Published eligible profile appears with stable public identity/routing IDs.
- Unsupported profile cannot publish with actionable reason.
- No secret/internal fields appear.
- 304 works and unrelated private changes do not incorrectly alter public representation.
- Catalog-read scope is enforced.

## Negative proof

- Missing/wrong scope denied.
- Internal provider ID, base URL, secret ID/name/value, configuration JSON and notes absent.
- Synthetic providers excluded.
- Routing ID from an unpublished profile is not listed.
- Malformed If-None-Match has safe behavior.

## Semantic invariants

- Only explicit eligible publications are discoverable.
- Catalog is a sanitized public projection.
- Public representation and routing do not expose internal provider identity.

## Evidence artifacts

At minimum:

- completed `proof/proof-manifest.json`;
- command transcripts under `proof/transcripts/`;
- changed-file inventory;
- architecture/reference artifacts;
- focused behavior artifacts;
- completed `SESSION-HANDOFF.md`;
- updated root `STATUS.md` and traceability rows.

## Progression gate

Pass only when every acceptance criterion, architecture assertion, focused build/test, and
negative proof is backed by an artifact. On pass mark this subbundle `DONE`, unlock only
`SB04`, and update the owning review.

On failure, keep downstream work locked. Do not call a missing proof a residual risk.

## Reopen triggers

- Relay support descriptors in SB04 require catalog shape change.
- Current optional-auth policy differs from prepared convention.
- Canonical JSON serializer/OpenAPI behavior changes.

## Execution checklist

- [ ] Current branch/commit/worktree captured.
- [ ] Mandatory skills loaded.
- [ ] Bundle and subbundle readiness validated.
- [ ] Dependencies are `DONE`.
- [ ] Before architecture/reference evidence captured.
- [ ] Scope implemented without widening.
- [ ] Affected production projects built.
- [ ] Test discovery recorded and nonzero.
- [ ] Focused positive/negative tests passed.
- [ ] Security/redaction checks passed where applicable.
- [ ] After architecture/reference evidence captured.
- [ ] Proof manifest completed with artifact hashes.
- [ ] Session handoff completed.
- [ ] Status/traceability/review updated.
