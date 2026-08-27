# SB06 semantic invariants

- Invariant ID: TOKEN-SCOPES
- Invariant ID: TOKEN-LIFECYCLE
- Invariant ID: TOKEN-ADMIN
- Invariant ID: TOKEN-PRIVACY
- Invariant ID: FRESH-5214
- Source raw note: N009/R9 and N010/R10 in bundle://inputs/05-compact-provider-and-token-administration.md.
- Expected behavior: exact namespace selection; searchable lazy token administration; durable revoke/delete enforcement; empty recoverable third client.
- Disallowed shallow implementation: hide token rows without denying use, silently widen empty scopes, fabricate old history, or reset other clients.
- Failing-first test: TOKEN_SCOPES_empty_selection_never_grants_broad_api_access failed before issuer repair; bundle://proof/SB06/regression-red.trx. Other new lifecycle behavior has explicit adversarial HTTP tests, not an invented pre-edit red run.
- Passing test: ApiTokenRegistryTests, ApiTokenAdministrationTests and ApiAccessAuthorizationIntegrationTests; raw TRX and the verified collection transcript are indexed in bundle://proof/SB06/manifest.md.
- Changed source files: bundle://proof/SB06/changed-files.csv; typed registry, authorized administration, JWT validation and focused Razor components.
- Production assertions: issue registers before return; signed managed tokens require an active stored ID; all administrative actions authorize; dialogs mount on explicit actions; no raw bearer storage.
- Red-team negative case: empty scopes, denied admin, corrupted/missing/deleted record, invalid signature and revoke-cancel followed by real HTTP requests.
- Downstream dependency check: live source token -> existing client's stored credential -> catalog discovery; fresh client remains unconfigured. bundle://proof/SB06/browser-validation.md.

- TOKEN-SCOPES: confirming scopes sets exactly those values; empty selection never
  grants api; cancel preserves text; catalog covers every declared scope.
- TOKEN-LIFECYCLE: all new JWTs are registered before release; revocation and deletion
  deny actual protected requests, including after storage reload. Missing/corrupt
  managed metadata denies access; signature/issuer/audience/lifetime/scope checks remain.
- TOKEN-ADMIN: list is lazily loaded and paged; unauthorized UI actions cannot list,
  issue, revoke or delete. Local trusted UI and explicit token administrators can.
- TOKEN-PRIVACY: registry/list/proof contain metadata, never bearer strings or signing keys.
- FRESH-5214: only the third instance is reset, with old DB/data recoverable; 5210/5212 preserved.

Shallow traps: hiding a row without denying JWT use; deleting a revocation tombstone
and reactivating a token; loading all records at page initialization; selecting none
and silently substituting api; pretending old stateless tokens have an issuance history.

Managed version claim + fail-closed registry requirement prevent deleted-token revival.
Legacy tokens remain subject to cryptographic validation and expiry; the UI states
their history is unavailable. Independent registry, component and real HTTP tests plus
Playwright MCP on Docker own proof. No provider fixtures or catalog mutations.
