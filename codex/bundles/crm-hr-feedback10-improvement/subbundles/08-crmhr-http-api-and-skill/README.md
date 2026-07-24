# SB08 CRM-HR HTTP API And Skill

## Status

- `Completed`

## Objective

- Expose the CRM-HR query and command paths needed to create, inspect, and evolve realistic party, workforce, skill, capacity, recruiting, interview, and lifecycle scenarios through the authenticated application API, then document the contract in a reusable repo-owned Codex skill synchronized to the active skill root.

## Covered Inputs

- Follow-up request items 4 and 5.
- New `R018`.

## Prerequisites

- Existing CRM-HR application services remain the canonical command owners.
- Global `/api` enablement and authorization policy remain owned by the Web API composition root.

## Exact Source References

- `repo://src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs`
- `repo://src/App/CanDoItAll.Web/Api/ApiEndpointResults.cs`
- `repo://src/App/CanDoItAll.Web/Api/ProjectsApi.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/CrmHrServices.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/PartyRecordQueryService.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Services/PartyDirectoryManagementService.cs`
- `repo://src/Modules/CanDoItAll.Modules.CrmHr/Recruiting/CrmHrRecruitingServices.cs`
- `repo://codex/skills/candoitall-api-agents`
- `repo://codex/skills/candoitall-api-processes`

## Deliverables

- A cohesive `CrmHrApi` endpoint group under `/api/crm-hr`.
- Typed request/query binding, normal cancellation, existing validation/error mapping, predictable not-found behavior, and existing global authorization.
- API component/integration tests for realistic positive and invalid-reference/validation cases.
- Repo-owned `codex/skills/candoitall-api-crmhr` containing concise workflow guidance and exact endpoint/request references.
- Active-root synchronization and hash validation for the new skill.

## Boundary Ownership

- Web owns HTTP transport, route binding, status mapping, and authorization composition.
- CRM-HR application services own business validation, persistence, audit/search side effects, and domain results.
- The API must delegate directly; it must not duplicate business rules or write EF entities.
- The skill documents and orchestrates the public contract; it does not bypass HTTP with database access.

## Dependency Impact

- No new project reference is expected because Web already references CRM-HR.
- No new application-service interface is justified solely for endpoints.
- SB09 cannot start until the live API contract and skill pass `CP-08`.

## Validation Depth

- Proof tier: `Behavioral`.
- Architecture checkpoint: `CP-08`.

## Implementation Steps

1. Inventory the existing service commands needed for the scenario and define the smallest coherent HTTP contract.
2. Map the `/api/crm-hr` endpoints through normal API authorization and error helpers.
3. Add endpoint tests covering successful round trips, invalid ids, and validation failures.
4. Initialize, author, and validate the CRM-HR API skill; keep detailed schemas in one direct reference file.
5. Synchronize the repo skill to the active root and verify matching hashes.

## Do Not Do

- Do not add direct EF writes, silent exception fallbacks, stringly typed command routing, application startup seed hooks, or authorization bypasses.
- Do not expose confidential notes or sensitive directory values in list responses.
- Do not invent a generic repository or a parallel CRM-HR business layer.

## Acceptance Checklist

- [x] Required party/workforce/recruiting commands are callable under `/api/crm-hr`.
- [x] Queries are bounded and sensitive list projections remain safe.
- [x] Invalid references and invalid models fail predictably with structured errors.
- [x] Business side effects come from existing application services.
- [x] The skill is concise, validated, synchronized, and sufficient for another Codex instance to operate the API.
- [x] Targeted tests and affected builds pass.

## Proof Required

- Semantic positive: create and retrieve a linked party/workforce/recruiting scenario solely through HTTP.
- Adversarial negative: nonexistent party/application references and invalid paging/model values return structured failure without partial persistence.
- Shallow-pass trap: endpoints returning acknowledgements without invoking canonical services, test-only endpoints, or skill prose that instructs direct database writes.
- Anti-stub audit: no TODO, `NotImplemented`, direct `DbContext`, or seed-only route remains.

## Progression Gate

- `CP-08` passed. The real-host HTTP round trip, invalid-reference/query negatives, skill validation/synchronization, affected build, and Release solution build agree. SB09 subsequently completed its API-only runtime scenario and final closure.

## Completion Record

- Shipped transport: `repo://src/App/CanDoItAll.Web/Api/CrmHrApi.cs`, `repo://src/App/CanDoItAll.Web/Api/CrmHrApiContracts.cs`, and the registration in `repo://src/App/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs`.
- Boundary proof: Web owns route binding and DTO mapping; handlers delegate to `PartyDirectoryService`, `IPartyRecordQueryService`, `PartyDirectoryManagementService`, `HrService`, and `RecruitingService`. The new Web API files contain no `AppDbContext`, direct `DbContext`, `IServiceProvider`, or `BuildServiceProvider` access.
- Semantic positive proof: `Api_round_trips_linked_hiring_and_workforce_scenario_with_bounded_pages` in `repo://tests/Integration/CanDoItAll.Tests.Integration/CrmHrApiIntegrationTests.cs` creates and reads a linked party, relationship, workforce, skill, application, interview, support, lifecycle, conversion, and capacity scenario through a real HTTP host.
- Adversarial negative proof: `Api_returns_structured_errors_for_invalid_references_and_query_validation` in the same file proves missing related parties, invalid recruitment data, and out-of-range paging fail with structured codes.
- Focused integration result: exit code `0`, `2 passed`, `0 failed`, `0 skipped` in `30s`; exact command is in `bundle://proof/final-validation.md`.
- Affected build result: worker-reported Integration project build completed with `0 errors`.
- Skill proof: `repo://codex/skills/candoitall-api-crmhr/SKILL.md`, `repo://codex/skills/candoitall-api-crmhr/references/api-contract.md`, and `repo://codex/skills/candoitall-api-crmhr/agents/openai.yaml` each passed the repository skill validator in both repo and active roots; corresponding file hashes matched (`ALL_HASHES_MATCH=True`).
- Full-build result: the final Release solution build completed with exit code `0`, `0 errors`, `165 warnings` in `31.39s`; warnings include the existing `System.Security.Cryptography.Xml` `10.0.7` `NU1903` advisories.
- Authorization scope: the routes inherit the existing conditional authorization on the parent `/api` group. The skill explicitly does not claim per-CRM-HR JWT scope enforcement because the current platform does not provide it.
- Anti-stub audit: no test-only route, direct persistence path, startup seed hook, `TODO`, `NotImplemented`, or hidden database workflow was found in the new API/skill surface.
- Closure decision: `Completed`; CP-08 may be trusted by SB09.

## Reopen Triggers

- Contract ambiguity, leaked sensitive data, authorization drift, direct persistence in Web, inconsistent errors, missing skill synchronization, or seed workflow requiring a database bypass.
