# 4. Configuration, HTTP contracts and migration

**All new option/DTO names below are proposed.** Existing names are marked explicitly. Examples become usable only after implementation and supplying real private secrets through deployment configuration.

## 4.1 Configuration contract

| Configuration | Default / semantics |
|---|---|
| `Api:Enabled` | Existing main business API mapping switch. Existing runtime/project-structure exceptions remain documented. |
| `Api:Authorization:Enabled` | Existing secure JWT versus intentionally open business API mode. |
| `Api:Authorization:Issuer`, `Audience`, `SigningKey` | Existing signing/validation configuration. Do not change values during this migration unless deliberately rotating credentials. |
| `Api:Authorization:DefaultTokenLifetimeMinutes`, `MaxTokenLifetimeMinutes` | Existing machine-token limits; retain 480/1440 defaults. |
| `Api:UserAuthentication:Enabled` | New, default `false`. Exposes login and self-session operations. Requires the main API and JWT authorization enabled. |
| `Api:UserAuthentication:TokenLifetimeMinutes` | New, proposed `60`; positive and within the configured upper bound. Not a replacement for machine TTLs. |
| `Api:AccessManagement:Enabled` | New, default `false`. Exposes HTTP user/machine-token administration. Requires user authentication and JWT authorization enabled. |
| `Api:BootstrapAdmin:UserName` | New, proposed default `admin`; a username is not a default password. Reserved against ordinary account creation. |
| `Api:BootstrapAdmin:PasswordHash` | New, no default. Complete hash in the agreed standard hasher format; required when user authentication is enabled. |
| CORS/trusted-proxy options | Follow current host option ownership. Defaults are restrictive; do not invent a parallel set if an existing validated host option fits. |

Environment-variable mapping follows the normal double-underscore configuration convention, for example `Api__BootstrapAdmin__PasswordHash`. An env hash is not a substitute for the JWT signing secret: they protect different things and must not be derived from each other.

Use startup validation and a consistent options lifetime. Route exposure is decided at startup; do not hot-reload a secret/master switch while endpoint metadata still describes a different mode. A configuration change requires a controlled restart. Never infer a permissive mode from invalid options.

## 4.2 Validated modes

| Profile | Main API | JWT auth | User auth | Access management | Expected behavior |
|---|---:|---:|---:|---:|---|
| Trusted open desktop/dev | On | Off | Off | Off | Existing intentionally open business behavior. No HTTP login/user/token administration routes. |
| Legacy/service JWT instance | On | On | Off | Off | Existing machine JWT calls work; local trusted UI can manage tokens. No new user-login surfaces or HTTP token issuance. |
| Login users, local administration | On | On | On | Off | Users log in and use assigned sections. Admin HTTP CRUD/issuance stays 404 even with an admin session. Local trusted administration remains available. |
| API-managed server instance | On | On | On | On | Login works; only the configured admin session may administer access over HTTP. Ordinary users get 403. |
| Main API disabled | Off | Either | Off | Off | New surfaces and ordinary main API absent. Existing separately mapped surfaces follow their documented rules. |
| Invalid combination | Any | Off | On or management On | Any | Startup validation fails; do not expose an unauthenticated sensitive endpoint. |
| Invalid exposure combination | Off | Any | On or management On | Any | Startup validation fails, rather than silently claiming login/admin are exposed. |
| Invalid dependency | On | On | Off | On | Startup validation fails. |
| Missing/malformed required admin hash | On | On | On | Either | Startup validation fails without logging the hash. |

This intentionally tightens the old `/api/access/tokens` behavior: previously, an issue-scoped bearer could mint arbitrary tokens and the open-mode route returned 400. After implementation the administrative gate defaults closed, producing 404, and an enabled surface also requires the verified configured admin session. Document this compatibility change prominently. It does not require invalidating machine tokens used for data/provider calls.

## 4.3 Route contract

Keep the existing error envelope conventions; statuses below are normative for authorization, while success shapes should fit existing typed API conventions.

### Public discovery and session operations

| Endpoint | Exposure / authority | Result |
|---|---|---|
| `GET /api/access/status` | Existing main API gate; anonymous sanitized status. | Keep existing safe fields; add booleans indicating login/admin exposure without account names, lists or secrets. |
| `POST /api/access/login` | Main API + user-auth gates; anonymous credential submission with throttling. | 200 with a newly issued registered session JWT; generic 401 invalid credentials; 429 throttled; safe 503 for unavailable required infrastructure. |
| `GET /api/access/me` | User-auth gate; current valid user/admin session. | Safe account identity, kind/admin indicator and effective scopes. No password/hash/credential-generation data. |
| `POST /api/access/logout` | User-auth gate; current valid session. | Revoke current session and return 204 or a documented acknowledgement. Other sessions are not revoked. |

Do not require a bearer to reach login because it happens to be below the authenticated `/api` group. Use an explicit anonymous declaration for that route only. A valid machine token cannot call user-self operations as an arbitrary user. No `/register` or password-reset-by-email route is introduced.

A proposed login body is exactly `{ "userName": "reader", "password": "<entered password>" }`. A proposed response contains `token`, `tokenType`, `expiresAtUtc`, `userId`, `displayName`, `scopes` and a safe `isAdministrator` boolean. The administrator ID can use a reserved stable representation distinct from ordinary GUID IDs; finalize the contract consistently. Do not expose persistence classes directly.

Sensitive successful responses and login errors should carry appropriate `Cache-Control: no-store`. A 401 bearer challenge must preserve `WWW-Authenticate: Bearer` and must not redirect to a login page. A valid credential without the required permission receives 403, not a fresh login token.

### Access administration

All routes in this table require `Api:AccessManagement:Enabled` and the verified configured-admin session. When the gate is closed they are absent/404. When enabled, no/malformed credential returns 401 and an ordinary/machine credential returns 403.

| Endpoint | Operation |
|---|---|
| `GET /api/access/scopes` | Catalog for access administration, with sections and sensitive capability labels. |
| `GET /api/access/users` | Bounded/paged safe user list; optional search. |
| `GET /api/access/users/{id}` | Safe details and concurrency version. |
| `POST /api/access/users` | Create ordinary user from username/display name/password/enabled/scopes. |
| `PUT /api/access/users/{id}` | Update permitted profile/enabled/scope fields with concurrency protection. |
| `POST /api/access/users/{id}/reset-password` | Replace password and invalidate existing sessions. Password is write-only. |
| `DELETE /api/access/users/{id}` | Remove ordinary user and invalidate sessions; no identity reuse. |
| `GET /api/access/tokens` | Existing registry metadata search exposed safely; bounded pagination. |
| `POST /api/access/tokens` | Existing machine-token issuer, now protected by the new administrative boundary. |
| `POST /api/access/tokens/{id}/revoke` | Revoke selected registered token. |
| `DELETE /api/access/tokens/{id}` | Delete token registration; token remains rejected afterward. |

The token list should distinguish machine credentials from sessions, or default to machine credentials with an explicit session filter. Never make the machine-issuance request accept a session kind or user binding. Ordinary users do not receive this listing or an API for issuing arbitrary machine tokens.

Use a consistent optimistic concurrency convention, such as an ETag/`If-Match` or an explicit expected version, across update/reset/delete. Return the repository's established conflict/precondition status on stale input and leave the current record untouched. A duplicate normalized username returns a documented conflict. Missing ordinary user returns 404. Never allow a stale profile update to restore a disabled account's old permissions or password.

New DTOs should reject unexpected privilege-bearing fields, including `isAdmin`, role, kind, hash, arbitrary subject or revision. Do not silently accept an overpost and later rely on a UI client never sending it. Limit lengths, list sizes and allowed scope catalog entries. Return validation errors without reflecting passwords or secret configuration.

The configured bootstrap administrator can be described separately in the administrative status/UI, but is not a writable ordinary user resource. Attempts to mutate it through ordinary CRUD are rejected. Do not add a second reset path that lets a user change the deployment admin's hash.

### Business API additions

The eight colleague endpoints listed in `02_UI_BRANCH_DELTA.md` require the main API gate and their own business scopes in secure mode. They are not automatically available to every authenticated caller. Workspace-defaults access is not the same permission as account/JWT administration.

Preserve current domain authorization after section checks. An account authorized for an API section is not automatically authorized for every existing object-specific operation in that section. Keep response/download/event aliases consistent with their originating operation's authority.

## 4.4 Service interfaces and composition

Suggested boundaries, not mandatory class-count requirements:

- Account store: private persisted records and atomic mutations, independent of active database profile.
- Account administration service: safe DTOs, username/scope/password rules, concurrency, audit and authority checks.
- Session service: login, user/admin resolution, registration, self-session projection and logout.
- Credential validator: cryptographic/registry/account checks, producing a verified request credential descriptor.
- Administrative access interface: host adapter for a verified HTTP admin or existing trusted local interactive operator.
- Scope catalog/policy registration: one authoritative permission vocabulary and per-operation decisions.

Prefer reusing existing `IApiTokenService`, `IApiTokenRegistry`, `ApiTokenAdministrationService`, clock and durable-file facilities. Internal signing can be factored to avoid two diverging JWT implementations. Do not expose an unrestricted “mint arbitrary principal” method to ordinary request handlers or tools. Inject the narrow operations the caller needs.

## 4.5 Deployment and migration sequence

First deploy the compatible code with user authentication and HTTP administration disabled. Retain the current signing key/issuer/audience and persisted `api-tokens` directory. Verify a pre-existing machine token still accesses its intended shared-provider surface and that revoked registrations still fail.

Prepare the configured admin hash with the documented local tool and mount the private control-plane directory durably. Supply a random signing key only for new installations; generating a new key on every process start would invalidate existing credentials. Do not put hashes/keys in checked-in `appsettings` examples or print configuration objects at startup.

Enable user authentication with JWT authorization enabled. Manage initial users through the trusted local Settings UI, or explicitly enable HTTP access management and log in as the configured admin. Verify the proxy excludes the SSR/interactive transport and the direct container port is not reachable by ordinary clients. Use a regular scoped account for everyday API-only UI work rather than retaining an administrator token in normal navigation.

Record the administrative issuance compatibility change for any existing automation that used `api.tokens.issue`; the operator must explicitly adopt the new approved administrative path. Do not add a secret backward-compatibility bypass. Existing machine data access is distinct and stays supported.

Test restart with the same control-plane root and configuration. Account state, token revocation and grants persist. Rotate the admin password hash without rotating the signing key and verify old admin sessions fail while unrelated machine tokens still work. Switching the active workspace database must not change the account store.

For rollback, preserve a backup of the private store and document the supported schema direction. An older host may reject new session tokens, which is acceptable; it must not treat them as legacy unrestricted tokens. Do not delete or rewrite old machine records to make rollback appear clean.
