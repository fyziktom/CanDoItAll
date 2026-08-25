# SB01 — Protocol, identities, routing IDs, and access context

State: `DONE`
Proof tier: `Governed`  
Depends on: `SB00`  
Next on pass: `SB02`

## Objective

Create the stable SDK-free contract boundary for catalog/inference ports, public identities, routing model IDs, capabilities, errors, and cross-cutting access context.

## Observable outcome

Server, client, Workspace, Web, and HTTP integration can depend on one lower-level contract without exposing internal provider profiles or creating reverse references.

## Inputs and current-state anchors

- Bundle root execution contract and architecture documents.
- Current repository state, not only the prepared SHA.
- Relevant source/test impact maps.
- Completed proof and handoff from every dependency.
- Current mandatory SharedInfo skills.

## Scope

- Add the SB00-approved SharedProviders Abstractions/Protocol project shape.
- Define protocol version and route constants for native catalog and OpenAI-compatible base.
- Define sanitized catalog/source/protocol/model/capability records with strict JSON contract rules.
- Define public publication/source-instance/routing model value objects and one routing codec.
- Define SDK-neutral catalog client and inference transport ports plus typed failure categories.
- Define relay operation/support descriptor records without HttpContext, EF entities, provider SDKs, or secrets in serializable DTOs.
- Add AccessContextReference, parser, scoped accessor contract, and Web binding/middleware.
- Use CanDoItAll-Access-Context-Ref and keep W3C tracing separate.
- Add project references and Composition/Web registrations only as required for the scoped context.
- Add serialization, round-trip, malformed-input, routing collision, and dependency guardrail tests.

## Out of scope

- No EF entities.
- No catalog database projection.
- No HTTP call to a central/upstream provider.
- No inference endpoint.
- No UI.

## Implementation sequence

1. Freeze exact public JSON names/version strategy before implementation.
2. Use immutable records/value objects and bounded validators.
3. Keep external OpenAI implementation quirks private to the later Http project.
4. Make route joining/base-path semantics explicit in contract tests.
5. Implement canonical public representation serialization inputs suitable for hashing, but do not persist publication yet.
6. Register access-context scoped state in the Web request pipeline without HttpContext dependency in inner contracts.
7. Update solution/project inventories and architecture guardrails.

## C# Architecture Impact

This subbundle is architecture-significant. Re-read
`architecture/00-csharp-current-state-inventory.md` through
`architecture/04-csharp-testability-plan.md`, update the affected checkpoint, and stop rather
than use a boundary workaround.

## Boundary Ownership

SharedProviders Abstractions owns public contract and ports. SharedKernel may own the generic access-context value/accessor if SB00 confirms cross-API placement. Web owns header binding only.

## Dependency Direction

Abstractions points only inward. Workspace/Web/Http may reference it later. It must not reference Workspace, Web, EF, Razor, MAF SDK, or provider SDK packages.

Record before and after `ProjectReference`/namespace direction even when no reference is
expected to change. A no-change result is still evidence.

## Pattern Decision

Ports and adapters, value objects, canonical serializer/codec, scoped request context.

Do not introduce an adjacent alternative pattern without reopening the owning ADR and
recording why the selected pattern failed.

## Testability Contract

All protocol/value/context behavior is pure or uses a minimal Web test host. Forbidden namespace/package tests are direct.

Every new behavior needs one realistic positive proof and one meaningful negative proof. Test
existence, file counts, status codes alone, or mocked self-assertions do not prove behavior.

## Partial Class Policy

New cohesive files only. No extension of large provider/runtime partials.

A large partial or monolithic file is a gate failure unless the architecture review documents
a narrow unavoidable reason.

## Architecture Proof Required

- Before/after ProjectReference tables and CodeAnalytics no-cycle evidence.
- Public API/type inventory and forbidden dependency scan.
- Serialization snapshots containing no internal/secret fields.
- Routing ID stable/collision/malformed vectors.
- Access-context valid/absent/malformed/oversized behavior.
- Web scoped isolation across concurrent requests.

## Test selection

| Topic | Owning project/lane | Stable filter | Planned expected discovery | Selection reason |
| --- | --- | --- | ---: | --- |
| `SharedProviderProtocolContractTests` | `tests/Solutions/CanDoItAll.Tests.Unit.slnx` | `FullyQualifiedName~SharedProviderProtocolContractTests` | 12 | Covers strict public serialization/version/capability contract. |
| `SharedProviderRoutingModelIdTests` | `tests/Solutions/CanDoItAll.Tests.Unit.slnx` | `FullyQualifiedName~SharedProviderRoutingModelIdTests` | 10 | Covers stable routing, ambiguity, malformed and privacy behavior. |
| `SharedProviderAccessContextTests` | `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | `FullyQualifiedName~SharedProviderAccessContextTests` | 10 | Covers parser plus real Web scoped middleware behavior. |

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

- Public DTOs cannot reference internal ProviderProfile or secret records.
- Routing ID distinguishes duplicate model names and does not expose internal profile ID.
- Access context is optional, opaque, bounded, request scoped, and independent from auth.
- Abstractions build and focused tests pass.
- Dependency graph remains acyclic.

## Negative proof

- Unsupported/unknown protocol version fails.
- Malformed routing ID fails closed.
- Malformed or multiple conflicting access-context headers return 400.
- A forged access context does not satisfy authentication or scopes.
- Forbidden namespace/package guardrail passes.

## Semantic invariants

- No serializable public type can contain upstream secret/internal profile data.
- Access context is not authorization.
- Inner project dependency direction is preserved.

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
`SB02`, and update the owning review.

On failure, keep downstream work locked. Do not call a missing proof a residual risk.

## Reopen triggers

- SB00 changes project shape or access-context owner.
- Current JSON/OpenAPI serializer cannot express the chosen strict contract.
- OpenAI SDK base-path requirements contradict route constants.

## Execution checklist

- [x] Current branch/commit/worktree captured.
- [x] Mandatory skills loaded.
- [x] Bundle and subbundle readiness validated.
- [x] Dependencies are `DONE`.
- [x] Before architecture/reference evidence captured.
- [x] Scope implemented without widening.
- [x] Affected production projects built.
- [x] Test discovery recorded and nonzero.
- [x] Focused positive/negative tests passed.
- [x] Security/redaction checks passed where applicable.
- [x] After architecture/reference evidence captured.
- [x] Proof manifest completed with artifact hashes.
- [x] Session handoff completed.
- [x] Status/traceability/review updated.
