# 39 External Cognitive Memory Security Isolation And Conformance

## Status

- `Completed`

## Execution Outcome

- The external service now fails closed without configured clients, authenticates and authorizes `/memory/*`, derives actor/project/access policy from claims, rejects malformed or unauthorized project scope, limits request size/rate, and filters lifecycle/access/redaction before recall materialization.
- The external implementation was modularized into cohesive Domain, Application, Persistence, Service, and Protocol owners; every handwritten production file is at or below 171 lines and the only partial is the conventional `Program` test-host seam.
- The external Contracts project owns dependency-free `CanDoItAll.CognitiveMemory.Protocol.V1` and HTTP DTOs. All cross-root project references and the inert external MAF project/tests were removed; raw hosted HTTP tests replace compile-time main-driver coupling.
- Local and copied-isolation builds completed with 0 warnings/0 errors and 59/59 tests. The isolated copy had no sibling `CanDoItAll` directory. Final CodeAnalytics snapshot `snap-20260712132721-cdf936e2` loaded nine projects with no blocking errors; Contracts has zero project/package references and no project-level cycle exists.
- Checked-in query/ingestion fixtures and process-start instructions provide the live seam. SB40 then passed actual main `NativeRemoteMemoryProviderDriver` interoperability against the separately launched authenticated service.

## Objective

- Make `CanDoItAll.CognitiveMemory` a genuinely external, authenticated, project-isolated provider whose wire contract and advertised capabilities conform to the generic memory protocol without compile-time coupling to the main repository.

## Success Criteria

- The Cognitive Memory solution builds and tests when the main `CanDoItAll` checkout is absent or renamed; no project references cross repository roots.
- Every provider operation is authenticated and authorized before domain access, except an intentionally minimal liveness endpoint.
- Project identity is validated, required where policy demands it, matched against caller claims, and never silently converted to global scope.
- Recall applies an explicit domain access policy before returning candidates, excluding unapproved, redacted, restricted, cross-project, or otherwise unauthorized records.
- The manifest advertises only operations that are implemented with truthful sync/async/status/event/feedback/source semantics.
- A running external service interoperates with the main generic HTTP driver through the versioned wire contract and secret-backed authentication.

## Covered Inputs

- R14
- R15
- R16
- R17
- R19
- R20
- R26
- R28
- R29

## Prerequisites

- SB38 completed with full typed context propagation, secret-safe HTTP configuration, capability honesty, and transport gates passing.

## Exact Source References

- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/02-csharp-dependency-direction.md`
- `bundle://architecture/03-csharp-pattern-selection-records.md`
- `bundle://architecture/04-csharp-testability-plan.md`
- `bundle://requirements/03-non-negotiable-boundaries.md`
- `repo://src/Memory/CanDoItAll.Memory.Abstractions/MemoryOperationEnvelope.cs`
- `repo://src/Memory/CanDoItAll.Memory.Abstractions/MemoryProtocolContexts.cs`
- `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.cs`
- `repo://src/Memory/CanDoItAll.Memory.Http/NativeRemoteMemoryProviderDriver.cs`
- `bundle://proof/SB39/transcripts/file-hashes.txt`
- `bundle://proof/SB39/transcripts/external-local-and-isolated-validation.txt`
- `bundle://proof/SB39/transcripts/codeanalytics-and-boundary-audit.txt`

## Deliverables

- Publish or generate a versioned, provider-neutral wire schema for Memory Protocol v1 and give the external repository its own strongly typed wire DTOs; remove sibling `ProjectReference` entries to main Memory Abstractions, Memory HTTP, and Agent Framework projects.
- Remove the inert native MAF contributor from service hosting and from the external solution unless it is rebuilt against an independently versioned abstraction; generic agent integration remains in the main Agent Framework Memory adapter.
- Add an ASP.NET Core authentication scheme backed by secret-configured provider credentials and an authorization policy that yields typed caller, tenant, project allowlist, agent, and runtime-session claims.
- Require authorization for the `/memory` protocol group; expose only a minimal non-sensitive liveness endpoint anonymously and keep readiness/provider details protected.
- Add request-size limits, rate limiting, cancellation/timeouts, safe problem details, and masked structured logging at the service boundary.
- Reject missing, malformed, or unauthorized project identity and invalid workspace scope with a typed protocol error; delete any mapping that silently substitutes global scope.
- Implement and register `ICognitiveMemoryAccessPolicy` as the single domain authorization seam for recall and other reads, using caller/project/session/source scope plus record lifecycle, review, access, redaction, and sensitivity state.
- Apply access policy before candidate materialization/mapping so unauthorized text, provenance, and scores never enter response construction or diagnostic logs.
- Propagate authenticated actor, agent, session, correlation, workspace, workflow/process, and project identity through application requests, store queries, traces, events, and audit records instead of fixed `native-recall`/`native-default` values.
- Correct the provider manifest and routes: remove or mark unsupported fake operation status, feedback, source-request, event, RCL, and advanced capabilities until durable implementations exist; do not return placeholder `501`/always-failed behavior for advertised operations.
- Ensure service and worker use consistent configured persistence, verify PostgreSQL migrations/readiness when PostgreSQL is selected, and prevent service/worker defaults from pointing at isolated in-memory databases in production.
- Add hosted authorization/access-policy tests and protocol schema conformance tests in the external repo, plus a process-level interoperability test that launches the external service and drives it with the main HTTP provider without adding cross-repo project references.

## Dependency Impact

- SB40 release proof depends on external-repo independence, endpoint security, project isolation, and honest protocol interoperability.
- Any sibling project reference or global-scope fallback here defeats the external-provider architecture even if local tests pass.

## Validation Depth

- `Security-critical external-provider and cross-repository boundary`

## C# Architecture Impact

- The external repository owns its domain/application/persistence/service layers and wire DTO implementation, while interoperability is defined by a versioned schema rather than shared source paths.
- Authentication and transport authorization stay in Service; domain record access decisions stay behind `ICognitiveMemoryAccessPolicy` in Domain/Application.
- The main repository sees Cognitive Memory only as an HTTP provider profile/manifest and never references its implementation assemblies.

## Boundary Ownership

- Cognitive Memory Domain owns record visibility rules and access-policy inputs/results.
- Cognitive Memory Application owns authenticated request orchestration and policy application.
- Cognitive Memory Persistence owns project-scoped queries and durable state, not authorization shortcuts.
- Cognitive Memory Service owns authentication, claims normalization, endpoint authorization, limits, and protocol mapping.
- The generic wire schema is provider-neutral; neither repository may add native domain fields to generic envelopes.

## Dependency Direction

- External direction: Service -> Application/Contracts/Persistence -> Domain, with Contracts remaining provider-wire focused.
- Main direction: host composition -> Memory HTTP -> Memory Application/Abstractions.
- Forbidden: any external project -> `../../../CanDoItAll/...`, any main project -> external Cognitive Memory project, Service -> main MAF, or Domain/Persistence -> ASP.NET Core authentication types.
- Protocol compatibility is verified at the wire boundary, not by compiling both repositories into one graph.

## Pattern Decision

- Use ASP.NET Core authentication/authorization middleware and a project-authorization requirement/handler for transport access.
- Use a domain Policy/Specification for record visibility because lifecycle, project, sensitivity, redaction, and caller claims must be composed and directly tested.
- Use an Anti-Corruption Layer mapper between versioned wire DTOs and native domain requests.
- Do not share implementation projects, use static API-key checks in endpoints, or rely on post-query response filtering.

## Testability Contract

- Authentication handlers use injected credential validation and clocks and can be tested with hosted requests and non-secret fixtures.
- `ICognitiveMemoryAccessPolicy` is directly testable across project, lifecycle, review, access, sensitivity, redaction, source scope, agent/session, and privileged cases.
- Store tests prove project predicates are applied in the query and unauthorized rows cannot reach the mapper.
- Process-level conformance launches the external executable on an ephemeral port and invokes it through the main HTTP driver using only JSON/HTTP and configured credentials.

## Partial Class Policy

- New authentication, authorization, access policy, mapper, and endpoint grouping types are non-partial and responsibility-named.
- Generated OpenAPI/schema clients and EF migrations may be partial; generated files must be identifiable and must not contain handwritten policy logic.
- Do not split `CognitiveMemoryProtocolApi` or access policy into capability-grouping partials.

## Architecture Proof Required

- Build the external solution after temporarily making the main checkout path unavailable, or run an equivalent project-reference audit plus isolated copy build proving zero sibling dependency.
- Capture project graphs for both repositories and prove no cross-root project edge or native implementation edge into the main host.
- Record threat-model cases for unauthenticated access, credential confusion, project swapping, global fallback, redacted/restricted recall, event leakage, oversized requests, and log disclosure.
- Run source audits for fixed actor/policy identities, permissive project parsing, anonymous `/memory` routes, raw credentials, post-materialization filtering, and overclaimed manifest capabilities.

## Implementation Steps

1. Turn SB35 unauthenticated/cross-project/global-fallback characterizations into hosted red tests.
2. Establish the versioned wire schema and remove all cross-repository project references and inert service-host MAF coupling.
3. Add credential validation, claims normalization, endpoint authorization, request limits, rate limiting, and safe errors/logging.
4. Implement the domain access policy and apply it before persistence results enter response mapping.
5. Propagate authenticated request context through native application, persistence, traces, and events.
6. Align manifest/routes with truly implemented behavior and align service/worker persistence/readiness.
7. Add hosted security, policy matrix, persistence, schema conformance, isolated-build, and process-level main-driver interoperability tests.

## Scope Exceptions

- Durable async operation status, feedback ingestion, source requests, RCL UI, or advanced native endpoints may be deferred only by removing their advertised capability and documenting a separately owned follow-up.
- A real production identity provider may remain host-configurable, but the shipped provider authentication boundary and project authorization must be functional and tested now.

## Do Not Do

- Do not make authorization optional in development or InMemory profiles used by conformance tests.
- Do not accept a caller-supplied project merely because it parses as a GUID.
- Do not filter sensitive records only after loading/mapping their content.
- Do not keep sibling project references for local convenience.
- Do not advertise placeholder, `501`, always-failed, in-memory-only, or unhosted capabilities.

## Acceptance Checklist

- Anonymous, invalid-key, expired/disabled-key, wrong-project, missing-project, and project-swapping requests are denied before engine/store access.
- An authorized project request returns only policy-approved records from that project.
- Restricted, redacted, unapproved, retired/rejected, out-of-scope source, and foreign-session records are excluded or denied according to explicit policy and never logged with content.
- Global scope is reachable only through an explicit authorized policy, never through parse/default behavior.
- Manifest and route behavior agree for every advertised capability.
- External service and worker share valid production persistence configuration and verify migrations/readiness.
- External solution builds/tests independently with zero main-repo project references.
- A launched external service succeeds through the main HTTP driver with valid credentials and fails predictably for invalid credentials/project context.

## Proof Required

- Create `proof/SB39/manifest.md` and `proof/SB39/semantic-invariants.md` with hashes, both repository revisions, and portable bundle transcript references.
- Failing-first proof: capture anonymous recall success, project-default/global behavior, unauthorized record visibility, sibling-reference dependency, and manifest/endpoint mismatch before repair.
- Positive proof: authenticated project-scoped recall through a launched service and main HTTP driver returns only approved records, with correlated audit/trace identity.
- Negative proof: run the full auth/project/access-policy matrix, malformed envelopes, oversized/rate-limited requests, unavailable persistence, and unsupported capability cases; assert engine/mapper zero-call where denial must precede access.
- Anti-stub proof: seed approved and forbidden records in real InMemory/EF stores, issue hosted HTTP requests, inspect captured main-driver envelopes and native audit traces, and demonstrate results differ by authenticated claims; direct mapper tests alone are insufficient.
- Run external isolated build/tests, PostgreSQL migration/readiness test where available, main HTTP driver tests, process-level conformance, dependency audits, secret/log scans, and architecture review.

## Browser Validation Logging

- N/A for native UI. This subbundle validates the provider service over hosted HTTP. If generic provider UI exposes health/auth diagnostics changed here, record the route and sanitized screenshots in SB40 instead.

## Progression Gate

- SB40 may start only after independent external build, zero sibling references, hosted authentication/project/access-policy tests, truthful manifest checks, process-level main-driver interoperability, security audit, and the SB39 architecture checkpoint pass.

## Suggested Agent Prompt

```text
Implement SB39 only. Make Cognitive Memory an independently buildable authenticated external provider, enforce project and record access before materialization, align the manifest with implemented behavior, and prove interoperability through a launched service without cross-repo project references. Stop on any security or architecture gate failure.
```
