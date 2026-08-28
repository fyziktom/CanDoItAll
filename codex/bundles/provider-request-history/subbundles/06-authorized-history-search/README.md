# SB06 — Authorized Bounded History Search

## Status

- Execution: `Not started`. This is an implementation contract, not completed feature evidence.
- Preparation: defined; entry requires the prerequisites below and renewed scope authorization.

## Objective

- Expose one authorized, bounded metadata query and separate detail/content/policy operations that serve both UI scopes without any eager source/provider reads.

## Covered Inputs

- N002–N009, N011; R002–R009, R011, R013, R014.
- [Normalized requirements](../../requirements/01-normalized-requirements.md).

## Prerequisites

- SB02 price semantics and SB05 canonical/coverage gate passed; SB03 persistence and SB04 identity remain valid.
- Host interactive/resource authorization and database-profile policy are identified; absence of authority must deny.
- Use existing in-process application/gateway composition; no new public HTTP API is needed solely for Blazor tabs.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Usage/ProviderUsageQueryService.cs`
- `repo://src/App/CanDoItAll.Web/Api/ApiManagedTokenValidation.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiScopeCatalog.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/SharedProviderAuthorizationIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/SharedProviderAccessContextTests.cs`
- `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/05-history-data-lifecycle.md`
- `bundle://architecture/09-search-security-contract.md`
- `bundle://architecture/10-pricing-and-capture-contract.md`

Linked source context:

[Existing aggregate API (do not reuse for search)](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Usage/ProviderUsageQueryService.cs).
[Managed-token validation](C:/repositories/CanDoItAll/src/App/CanDoItAll.Web/Api/ApiManagedTokenValidation.cs).
[Existing scope constants](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiScopeCatalog.cs).
[Authorization integration fixture](C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/SharedProviderAuthorizationIntegrationTests.cs).
[Untrusted context fixture](C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/SharedProviderAccessContextTests.cs).
[Production composition](C:/repositories/CanDoItAll/src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs).
Normative [boundary map](../../architecture/01-csharp-boundary-map.md),
  [lifecycle](../../architecture/05-history-data-lifecycle.md),
  [query/security](../../architecture/09-search-security-contract.md) and
  [pricing/capture](../../architecture/10-pricing-and-capture-contract.md).

## Deliverables

- Typed AllAuthorized/SingleProvider query with UTC interval, exact model, workload/operation/outcome/price and safe caller/request filters; all partition/authority comes from host.
- Implement scalar AsNoTracking indexed search ordered by immutable SortAtUtc+EntryId with Take(page+1), page50max200/range31days/deadline10s and no automatic Count.
- Protect cursor bindings to applied filters, stable partition and current authorization/profile generation; document live membership and explicit Refresh.
- Separate metadata read, content read and policy manage grants; per-owner content authorization and before-publish recheck for all asynchronous results.
- Explicit metadata/detail/content/policy service operations with sanitized failures and projection coverage; no body/config/token fields in row DTOs or logs.
- Define safe local-operator policy through existing trusted host mechanism; legacy/unavailable caller attribution never implies authorization.

## C# Architecture Impact

Application orchestrates neutral access/read/source ports; Persistence runs one bounded scalar query. Concrete source detail readers stay with owners and are invoked only after authorization.

## Boundary Ownership

Web owns trusted principal/resource evaluation and before-publication fence. Owner adapters own canonical content; protected standalone detail stays in Persistence. UI never authorizes by hiding controls.

## Dependency Direction

No Application-to-Web/Workspace/owner or UI-to-EF dependency. A current module's persistence adapter reference does not authorize its Razor/controller code to use concrete persistence types.

## Pattern Decision

ADR05/08: real policy/read boundaries and live keyset paging. Avoid a long-lived multi-page transaction, insertion watermark allocator, raw SQL body search or generic repository façade.

## Testability Contract

New ProviderHistoryQueryTests/ProviderHistoryQueryIntegrationTests for new contracts; extend existing authorization cases. Proposed cases: No_query_without_explicit_call; Cursor_is_bound_to_scope_and_filters; Equal_timestamps_page_by_entry_id; Permission_or_profile_change_before_publish_discards_result; Metadata_grant_does_not_open_owner_content; Query_never_reads_bodies_or_source_files.

## Partial Class Policy

No new runtime partial. Existing Razor code-behind/generated files are exceptions only for
their established framework role. New cohesive classes follow the 250-line review and
400-line redesign/exception gate; extraction removes the original behavior.

## Architecture Proof Required

- Record actual changed files, public signatures and project edges against the allowed
  dependency table. Review DI factories and old call sites, not only the new collaborator.
- Capture generated SQL and authorized/denied query plans, inspect every DTO/log field, and test production policy registration including missing local-operator authority.

## Dependency Impact

- SB07 uses these ports in both tabs/settings; any paging/security/coverage change invalidates its controller/component proof.
- SB08 verifies scale, actual hosted authority transitions and cross-instance credential investigations.

## Validation Depth

- Proof tier: `Governed`.
- Critical foundation: Yes; authorization, tenant/profile isolation, data minimization and query bounds..
- Test project/filter: `C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` / `FullyQualifiedName~ProviderHistoryQueryTests|FullyQualifiedName~ProviderHistoryAuthorizationTests|FullyQualifiedName~ProviderHistoryPolicyTests`; `C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` / `FullyQualifiedName~ProviderHistoryQueryIntegrationTests|FullyQualifiedName~ProviderHistoryAuthorizationIntegrationTests|FullyQualifiedName~SharedProviderAuthorizationIntegrationTests|FullyQualifiedName~SharedProviderAccessContextTests|FullyQualifiedName~ApiAccessAuthorizationIntegrationTests.API_BOUNDARY_local_operator_ui_identity_does_not_authenticate_http_boundaries`.
- Selection reason: New query/security/policy behavior, dedicated history host-authorization integration, existing invoke/catalog/context regressions and the real local-operator HTTP boundary. ProviderHistoryAuthorizationIntegrationTests is proposed; its metadata/content and before-publication fence tests must be discovered and executed, not replaced by legacy inference authorization tests.
- Expected discovery: InvokeOnlyScope_NativeCatalog_ReturnsNativeForbidden, ForgedAccessContext_DoesNotSatisfyAuthentication and API_BOUNDARY_local_operator_ui_identity_does_not_authenticate_http_boundaries, plus the six proposed query cases, Metadata_and_content_require_separate_explicit_permissions, Revocation_or_profile_change_after_query_denies_publication, Source_deletion_or_expiry_before_detail_publication_denies_content, Policy_changes_require_explicit_apply_and_concurrency_control, denied/missing/expired owner detail and invalid range/enum/cursor fixtures. Record exact actual cases/counts at execution;
  zero discovery or a missing named expected case fails the gate. Discovery has not run now.
- Invalidation keys: HistoryQueryV1; CursorBinding; MetadataContentManagePolicies; BeforePublishFence; QuerySqlShape; OwnerContentAuthorization.
- Broad-gate decision: Required once at frozen SB08 only if public-contract/schema/DI
  changes made here trigger it. No broad suite here or repeated run without invalidation.
- Future focused commands (after implementing the named cases; use the same unchanged
  source revision for discovery/build and the subsequent no-build execution):

```powershell
dotnet test 'C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj' --list-tests --filter 'FullyQualifiedName~ProviderHistoryQueryTests|FullyQualifiedName~ProviderHistoryAuthorizationTests|FullyQualifiedName~ProviderHistoryPolicyTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj' --no-build --filter 'FullyQualifiedName~ProviderHistoryQueryTests|FullyQualifiedName~ProviderHistoryAuthorizationTests|FullyQualifiedName~ProviderHistoryPolicyTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj' --list-tests --filter 'FullyQualifiedName~ProviderHistoryQueryIntegrationTests|FullyQualifiedName~ProviderHistoryAuthorizationIntegrationTests|FullyQualifiedName~SharedProviderAuthorizationIntegrationTests|FullyQualifiedName~SharedProviderAccessContextTests|FullyQualifiedName~ApiAccessAuthorizationIntegrationTests.API_BOUNDARY_local_operator_ui_identity_does_not_authenticate_http_boundaries'
dotnet test 'C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj' --no-build --filter 'FullyQualifiedName~ProviderHistoryQueryIntegrationTests|FullyQualifiedName~ProviderHistoryAuthorizationIntegrationTests|FullyQualifiedName~SharedProviderAuthorizationIntegrationTests|FullyQualifiedName~SharedProviderAccessContextTests|FullyQualifiedName~ApiAccessAuthorizationIntegrationTests.API_BOUNDARY_local_operator_ui_identity_does_not_authenticate_http_boundaries'
```

## Implementation Steps

1. Implement validation and trusted scope/permission derivation before any database/source reads.
2. Implement metadata-only indexed query and live keyset cursor, explicit coverage and bounded concurrency/deadline.
3. Add separate authorized owner/detail/policy operations with read-time expiry and safe rendering DTOs.
4. Recheck current authority/profile before publishing; discard results from stale generation even if the database call completed.
5. Verify SQL, forbidden data absence, real host policy composition and keyset changes under backfill/retention.

## Acceptance Checklist

- [ ] No unfiltered aggregate, file enumeration, N+1 owner/key lookup, body hydration or provider/catalog call appears in search.
- [ ] No query can widen a host-fixed provider or cross stable partition/security boundary.
- [ ] Metadata/content/manage are independent; invoke-only and missing authority cannot read history.
- [ ] Expired/unauthorized content is unavailable even if a row/cursor/request ID is known.
- [ ] Cursor ordering uses immutable keys with honest live Refresh behavior and explicit legacy TimeBasis.
- [ ] Auth/profile changes during await are checked server-side before publishing, not just by the UI.

## Proof Required

- Store a proof manifest, exact command transcripts, discovered cases/exit codes, changed-source revision, artifact paths/hashes and semantic positive/negative evidence under `proof/SB06/` at the bundle root.
- Include generated SQL and PostgreSQL EXPLAIN artifacts for provider/time, credential/time and all-authorized pages; positive/negative scope/cursor/content tests and payload/log inspection. Record actual bounds and plans, not a claimed benchmark speedup.
- Follow [validation strategy](../../plan/02-validation-strategy.md); distinguish existing
  test anchors from proposed new cases, and source proof from executed behavior.

## Browser Validation Logging

N/A for direct UI changes in this phase. Production host/SQL/lifecycle proof is required where listed; the two-tab desktop acceptance remains SB07/SB08.

## Scope Exceptions

- This phase alone does not close the complete product request. Deferred IDM/EGCP person
  mapping, global federation, exact wire replay, mobile redesign and unrelated refactors
  remain outside the bundle.
- No paid inference, user-database mutation or deployment without explicit authorization.

## Do Not Do

- Do not add public API endpoints, arbitrary remote log access or full-text body search merely for the UI.
- Do not copy an authentication-disabled allow-all shortcut or trust claims/context that were not validated.
- Do not compute totals/historical facet lists automatically or relabel prior results as a new draft query.

## Progression Gate

- SB07 may start after real policy composition, bounded SQL/cursor behavior, separate content authority and before-publication fence pass; denied/empty/incomplete results are distinguishable.
- Update [execution report](../../reviews/01-execution-report.md) with actual proof and
  downstream dependencies checked. A planned command or passed intermediary is not closure.

## Reopen Triggers

- Query/filter/sort/permission changes, new content owners or auth/profile semantics invalidate query and both UI/runtime acceptance gates.
