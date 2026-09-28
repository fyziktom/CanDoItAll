# 1. Current implementation and gaps

**Status:** source inspection of the pinned development commit; not runtime penetration testing. Evidence IDs refer to `07_SOURCES_AND_LIMITS.md`.

## 1.1 Existing building blocks worth preserving

`ApiAccessOptions` in Workspace/ApiAccess already separates business API mapping, OpenAPI and Swagger switches from `Api:Authorization:Enabled`. The reviewed defaults are API/OpenAPI/Swagger enabled and JWT authorization disabled. The signing key must contain at least 32 UTF-8 bytes when authorization is enabled. The default machine-token lifetime is 480 minutes with a 1440-minute maximum. A minimum byte count is not proof of key entropy: deployment instructions should generate a cryptographically random key, not a predictable 32-character phrase. [D01]

The issuer produces HS256 JWTs with issuer, audience, subject, display name, timestamps, a `jti`, `cda_token_version=1`, and both a space-delimited `scope` and a `scopes` collection. It registers metadata before returning the token. Listing records cannot retrieve the original bearer value. Preserve these machine-token semantics unless a separately justified, tested change is needed. [D01]

`ApiServiceCollectionExtensions` installs JWT Bearer validation of issuer, audience, signature and lifetime, with 30 seconds of clock skew. Its validation event checks the managed-token registry. Authentication registration and the host authentication/authorization middleware are conditional on authorization being enabled. This must be taken into account when adding login and admin options; adding endpoint metadata without the correct configured pipeline is insufficient. [D02, D10]

The registry is not a business database table. `FileApiTokenRegistry` stores one versioned JSON record per token in `api-tokens` under `IControlPlanePathResolver`. It uses `DurableFileWriter.Private`, create-new semantics for issuance, per-token mutation coordination, and private directory creation. The current record validator rejects empty scopes. This is the natural persistence pattern for small instance-local API users, without adding an EF Identity schema or coupling login to the selected workspace database. [D04]

`ApiTokenAdministrationService` already protects issuance, search, revoke and delete through `IApiTokenAdministrationAccess`. The Web adapter accepts a trusted interactive local-operator principal or an authenticated bearer with `api.tokens.issue`. The `/api/access/tokens` HTTP endpoint currently calls the lower issuer directly and uses its endpoint scope policy rather than that administration service. New HTTP routes should not create a second, weaker administrative path. [D03, D05]

Settings already contains the `api-access` tab, `ApiTokenAdministrationPanel`, `ApiScopePickerDialog` and `ApiTokensDialog`. The panel supports one-time token display and metadata management. Reuse these capabilities; add user administration and configuration state instead of replacing the Settings experience. [D08, D09]

## 1.2 Distinguish three existing boundaries

**Authentication:** is the bearer cryptographically valid and, when marked as managed, still registered and active?

**API permission:** does it have the exact required scope or the permitted broad `api` alternative for this endpoint?

**Domain permission:** does the operation's existing resource/owner/approval policy permit this actor and effect?

The new work must complete the second boundary and add account liveness to the first. It must not replace the third with an administrator-wide bypass.

## 1.3 Findings and implementation consequences

### F01 — Sensitive HTTP token issuance has no independent exposure switch

`POST /api/access/tokens` is mapped whenever the main API is mapped. In secure mode it requires `api.tokens.issue`; broad `api` alone is insufficient. In open mode the route is mapped without that scope policy, but the issuer rejects the operation because JWT authorization is disabled. The observed open-mode behavior is therefore a 400, **not anonymous successful token issuance**. [D01, D03, D12]

Required change: make all HTTP access-administration routes independently opt-in and admin-only. With the new gate closed the routes should be absent/404, including the existing issuance route. Keep direct trusted-operator token administration available in secure local mode even when its HTTP equivalent is disabled.

### F02 — A valid narrow bearer can still pass general API families

The `/api` parent group requires authentication, not the `GeneralApi` scope policy. For example, the reviewed workflow family explicitly documents that any valid bearer can use its general endpoints. Other families need inventory at implementation time. Existing policies on memory, chats, project structure, provider history and shared providers have different explicit/broad semantics. [D02, D03, D06, D11]

Required change: each ordinary API operation needs an explicit section decision. Merely storing a selected section list in a user or JWT is not enforcement. Do not put `RequireAuthorization(GeneralApi)` on the parent group: policies combine, so that would also reject legitimate narrowly scoped callers to child endpoints. Use appropriate per-family/per-operation metadata and policy composition.

### F03 — Unmarked legacy JWTs deliberately bypass the registry lookup

`ApiManagedTokenValidation` returns without registry lookup when `cda_token_version` is absent. Such a token still has to pass JWT signature/issuer/audience/lifetime validation. For managed v1, unsupported version, invalid token ID, missing/revoked/expired/deleted record or registry failure denies authentication. [D07]

Required change: retain a deliberate legacy path, but never treat a legacy/machine token as a user or administrator. New user sessions require a typed record and current account validation. Do not describe legacy tokens as individually revocable through a registry they never entered. Their current expiry/signing-key lifecycle is a separate compatibility limitation.

### F04 — The current scope parser is not a permission catalog validator

Issuance trims and deduplicates scopes case-insensitively; runtime scope matching is ordinal and case-sensitive. Issuance does not validate known scope names. Explicit null subject/display name or null list elements can reach `.Trim()` and fail outside the intended validation exception handling. [D01, D06]

Required change: one catalog supplies canonical scope names, privilege classification and selectable groups. New writes normalize to canonical known entries and reject malformed/unknown/privileged overposting. Preserve old token interpretation, rather than retrospectively changing case sensitivity or treating unknown old scopes as broad access.

### F05 — Local operator trust already has a meaningful defensive boundary

`LocalOperatorAuthenticationStateProvider` has an internal authentication type and uses both the original socket peer address and effective address. It captures trust for an interactive circuit; a missing HTTP context is not trusted. It does not replace the visible SSR authentication state with a global login policy. The development host captures the original peer before forwarded-header processing in all environments. [D10, D13]

Required change: preserve this boundary and test it. Do not implement new administration as `HttpContext is null => admin`, `loopback => admin` for HTTP, an HTTP header saying “local”, or a catch-all ambient service principal. Local UI and HTTP identities need explicit adapters, not a fallback that elevates an uninitialized scope.

### F06 — Forwarded-header trust is too broad to become a new security assumption

The host clears `KnownIPNetworks` and `KnownProxies`. Its original-peer check partly protects the existing interactive trust path, but new rate limits, TLS decisions or host-based checks must not assume forwarded headers are authentic from arbitrary peers. [D10, S04]

Required change: support explicit trusted proxy configuration and preserve original-peer capture. Separate trusted proxy transport from trusted operator UI addresses. Do not trust an entire VPN/subnet as an operator merely because clients are allowed to reach the API. Test direct and proxied paths, including forged forwarding headers.

### F07 — `Api:Enabled` is not currently a universal host HTTP kill switch

Main API mapping respects `Api:Enabled`. The reviewed status documentation explicitly notes exceptions for project-structure/runtime routes; documentation mapping itself checks OpenAPI configuration separately. Program also maps managed files and the SSR host separately. [D01, D03, D10]

Required change: every **new** login/admin/settings route obeys the appropriate API switch and cannot become another exception. Inventory and document existing exceptions instead of silently promising that `Api:Enabled=false` removes every route. If a stricter global switch is introduced, preserve internal runtime callers or provide a deliberate migration; it is not an incidental auth refactor.

### F08 — Correct errors need the complete production pipeline

The host uses status-code re-execution into `/not-found` and an exception handler targeting `/Error`. An isolated handler test does not prove that disabled routes or API failures remain useful API responses. [D10]

Required change: API failures must retain their 401/403/404/429/503 status and a safe API body rather than becoming successful SSR content or redirects. Tests should disable client auto-redirects and check content type, status, body and absence of writes.

## 1.4 Existing scopes to preserve

| Existing scope | Compatibility note |
|---|---|
| `api` | Broad ordinary API access only where explicitly supported; not universal administrator authority. |
| `api.tokens.issue` | Existing privileged issuance grant. New remote access administration additionally requires the new gate and administrative session. |
| `api.memory-providers.read`, `.write`, `.query` | Preserve the existing broad-or-specific semantics. |
| `api.project-structure.write` | Preserve lease/caller binding and existing checks, including reads historically grouped under this capability. |
| `api.llm-chats.read`, `.manage`, `.execute` | Keep distinct existing exact permissions. |
| `api.workflows.respond` | Preserve exact human-response privilege and caller/owner checks. |
| `api.shared-providers.catalog.read`, `api.shared-providers.invoke` | Preserve actual client/server-provider operation and publication policy. |
| `api.provider-history.read`, `.content.read`, `.manage` | Keep metadata/content/policy distinctions and canonical-owner checks. |
| `api.storage-placement-recovery.read`, `.reconcile`, `.verify-external-termination` | Keep stronger storage recovery authority and project permissions. |

Names above are observed, not a proposal. New section scopes should be explicit additions, not renames of existing ones. [D14]
