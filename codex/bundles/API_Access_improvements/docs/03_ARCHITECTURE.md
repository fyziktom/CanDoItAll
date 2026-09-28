# 3. Target architecture and bounded decisions

Everything in this document is a **proposal for implementation**, unless explicitly described as an existing behavior. Adjust internal names to current repository conventions; preserve the security semantics.

## 3.1 Scope and threat model

The instance runs on a PC or behind a controlled corporate/VPN proxy. Treat a caller who can reach an enabled HTTP API as potentially untrusted with respect to other API sections and administration. Internal network placement does not imply administrator privilege.

The deployment operator, host configuration, signing key and protected local operator UI are trusted. This change does not defend against an attacker who already controls the host, reads the signing key, replaces the private control-plane store or controls the configured trusted proxy. It is not a multi-tenant isolation project.

API scopes restrict entry points. They do not by themselves sandbox everything an authorized workflow/agent can do through its configured tools. Preserve existing tool/capability/approval boundaries and do not give ordinary runs a bootstrap-admin credential. Explain powerful execute/configuration permissions in the picker. Do not claim per-user privacy for every object where the application currently shares data; existing domain checks remain the boundary.

## 3.2 One authentication stack, distinct credential kinds

Retain JWT Bearer as the HTTP authentication mechanism. Do not add a cookie fallback for API endpoints or a second web identity framework merely to support local accounts.

Model the following server-recognized credential kinds:

| Kind | Issuance and verification | Administrative authority |
|---|---|---|
| Legacy machine JWT | Existing unmarked JWT validation path. | None by default, regardless of role/subject naming. |
| Registered machine JWT | Existing v1 issuer and token registry; current revocation semantics. | No new access-admin authority from `api`/`api.tokens.issue` alone. |
| API user session | Password login; new versioned managed token plus user ID/revision binding. | Ordinary account scopes only. |
| Configured admin session | Password login against the configured administrator hash; bound to current configured credential. | Access administration, when its HTTP exposure gate is open. |
| Trusted local operator | Existing server-side interactive capability; no bearer is synthesized for HTTP. | Direct local administration through its explicit adapter. |

A practical wire choice is to retain `cda_token_version=1` for machine issuance and add version `2` for the two session kinds. Keep the existing JWT `typ` convention unless tests justify a change. Explicit version/kind dispatch and server-side registration provide the separation; an arbitrary type claim is not authoritative. Unknown combinations fail closed. JWT best practice requires separating token validation contexts rather than trusting claims from another token class. [S01, S05]

Extend registry metadata additively with a credential kind and optional account binding. Old documents must still deserialize and validate as machine records. New session metadata is never returned as a raw persistence object. Avoid changing every machine token to v2, changing the signing key, or relabeling existing subjects as accounts.

For user sessions the persisted metadata and signed claims must agree on ID, subject, kind, granted scopes, account ID and authentication revision. Also validate required expiry/issuer/audience/signature/algorithm and registration status. Reject duplicate/conflicting security-critical claims and malformed variants. Restrict accepted algorithms explicitly to the configured supported algorithm; do not let a token choose a different verification scheme.

## 3.3 Minimal account model

A proposed persisted `ApiUserRecord` has:

- immutable `Id` (GUID) and normalized unique username;
- display username and display name;
- `Enabled`;
- salted versioned `PasswordHash`;
- canonical `AllowedScopes` for business API capabilities;
- monotonic `AuthenticationRevision` for invalidating old sessions;
- `CreatedAtUtc`, `UpdatedAtUtc` and a concurrency/document revision.

Do not add PartyId, organization ownership, tenant membership, email confirmation, billing, a role editor or a foreign key into the currently selected application database. A name change never changes the stable user ID or lets a new account inherit the deleted account's sessions.

For a predictable first version, normalize usernames with a documented invariant/case-insensitive rule and restrict login names to a small unambiguous character set, for example ASCII letters/digits plus `.`, `_`, `-`, `@`, with a bounded length. Display names can be Unicode. Reserve the configured administrator username after the same normalization. Passwords are not trimmed or case-normalized. A proposed initial password policy is 12–256 characters, allowing passphrases and Unicode, with no silent truncation; use the same rule in creation, reset and the local hash helper. Reject control characters in display/log-facing fields; never construct filesystem paths from usernames.

A single configured administrative identity is sufficient. Ordinary persisted API users are non-admin. Their create/update DTOs do not include `IsAdmin`, arbitrary roles, caller identity, `AuthenticationRevision`, password hashes or token-kind fields. Creation and password reset take a write-only password. A generic user edit never accidentally replaces a password with a blank value.

## 3.4 Persistence without a new database subsystem

Use the existing control-plane root and `DurableFileWriter` security/durability conventions. A small `FileApiUserStore` with one schema-versioned private document is a reasonable default: one coordinated read/check/write operation makes username uniqueness and revision updates atomic. Per-user files are also acceptable if uniqueness/index consistency is solved without fragile partial writes.

The control-plane persistence contract/record belongs alongside current infrastructure control-plane contracts. ApiAccess application services can remain in the current `CanDoItAll.Modules.Workspace.ApiAccess` owner to avoid a broad relocation during merge preparation. Keep them free of Razor, `HttpContext` and endpoint routing. Web implements the caller-authority adapter and maps transport DTOs. Foundation must not reference Workspace.

Do not use a new SQLite database: current repository guidance specifically does not make SQLite a base runtime dependency. Do not put account authentication into `Workspace_Settings` or an active database profile. Switching profiles must not change who can log in or resurrect deleted credentials.

Missing/corrupt/unsupported credential records fail closed. A malformed store must not be silently overwritten by an empty one. Log safe operational diagnostics. Test cross-process/coordinated username creation, interrupted writes, restart, private file permissions on Linux and Windows behavior. A single active application process per control-plane root is the deployment target; distributed multi-host identity replication is not being promised.

## 3.5 Passwords and the configured administrator

Use `PasswordHasher<T>` from the framework, independently of the full Identity user-management stack. Microsoft recommends the high-level hasher rather than manually assembling a password store from low-level PBKDF2 primitives. Use a documented, benchmarked work factor and versioned hashes; OWASP currently gives separate iteration guidance for the selected PBKDF2 PRF. Do not equate the framework default with an automatically sufficient policy or invent a custom fast hash. [S02, S03]

A reasonable starting policy with the current V3 HMAC-SHA512 implementation is at least the reviewed OWASP work factor (220,000 iterations), verified against the actual package/runtime in this repository. If the runtime implementation differs, select the matching policy rather than copying the number blindly. Benchmark on representative deployment hardware and bound login concurrency. Store the salt and algorithm/work parameters in the hasher's standard format. Support success-with-rehash for persisted users without losing concurrent scope/password changes.

Provide a tiny local hash-generation command/tool that uses exactly the same hasher/options as login. Read passwords without terminal echo and not from a command-line argument. The tool may print the resulting hash for deliberate operator use but never the password. Do not hard-code demonstration hashes or example passwords in deployment defaults.

Proposed bootstrap settings are username and password hash. Secure login enabled without a valid configured administrator is a startup error, not a reason to create `admin/admin` or turn authentication off. Ordinary users can be provisioned locally before remote administration is exposed. The configured admin record is read-only in UI/HTTP account CRUD: replacement/reset is a deployment operation. Its current credential binding must be checked for every admin session, so replacing its hash and restarting invalidates old admin sessions without changing the JWT signing key or invalidating machine tokens.

A server-private fingerprint of the configured credential stored with the admin session, or a persisted credential-generation value maintained when configuration changes, can implement this binding. Never expose the hash/fingerprint in JWT payloads or DTOs. Do not require the operator to remember to increment a separate version manually for security to work.

The default development convenience is **disabled new login/administration**, retaining the existing open business API mode, or a real developer-configured hash for testing the secure mode. No mock admin handler is required. If an existing test-only fake is useful, it stays in test composition, never in the shipped production fallback path.

## 3.6 Login, re-login and session revocation

`POST /api/access/login` accepts only username and password. The server resolves the principal kind and current permitted scopes and issues a registered session token. The client cannot request another subject, admin role, arbitrary scopes or an unlimited lifetime.

Use a separate configured user-session lifetime, proposed default 60 minutes, without changing existing machine-token lifetime defaults. The UI stores the bearer in memory where practical and returns to login after expiry. Passwords are not retained in browser storage to implement automatic re-login. This directly solves “admin must send a new JWT” without creating a refresh-token store, rotation races or an OAuth server.

`GET /api/access/me` returns the authenticated session user's safe identity and current effective scopes. `POST /api/access/logout` revokes only the current registered session. Neither endpoint lets a user administer another user's tokens. Machines use the existing API contract, not the user session endpoints.

Allow zero business permissions without accidentally falling back to `api`. Because the current registry requires at least one scope, a concrete small option is a reserved `api.session` capability automatically added to new sessions. It only enables self-session endpoints and has no business or administrative wildcard effect. It is not user-selectable. Alternatively adapt the registry's session validation explicitly, while preserving its machine-token empty-scope rejection. Pick one approach and test it; do not weaken the existing regression test to accept broad defaults.

When a password is reset, a user is disabled/deleted, or business permissions change, atomically update the authentication revision or remove the account. Every subsequent user-session authentication checks the current record and revision. Re-enable does not restore a previous revision. A concurrent login/reset cannot produce a usable token with superseded rights. Already admitted work is not transactionally rolled back; existing cancellation/approval behavior still governs in-flight effects.

For long-lived event streams, a handshake check alone is insufficient. Check expiry and current authorization at a bounded heartbeat/event interval and close the stream on revocation. Do not keep sending buffered sensitive events after recognizing revocation. Document the bounded interval (the existing 15-second heartbeat is a natural starting point), and do not claim instantaneous cancellation of an already-running external effect. Do not introduce a perpetual generic polling job for every account.

Use generic invalid-login responses for unknown user, wrong password and disabled account, with comparable password-hash work for nonexistent accounts. Bound body/field sizes, concurrent expensive hashing, and failed attempts. Apply a bounded per-client/per-normalized-user throttle with safe retry responses, not a permanent account lockout trivially triggerable by another VPN user. Do not log credentials. Infrastructure failure is not a successful fallback login. [S02, S03, S04]

## 3.7 Authorization and the capability catalog

Extend the existing scope catalog with a small set of section capabilities. Examples for previously general-only families are `api.projects.read/write`, `api.agents.read/write/execute`, `api.workflows.read/write/execute`, `api.processes.read/write/execute`, `api.prompts.read/write`, `api.crm-hr.read/write`, `api.plugins.read/write` and `api.settings.workspace.read/write`. Resolve actual route groups before finalizing names. Reuse existing precise scopes instead of renaming or duplicating them.

One catalog should describe canonical name, section, label, description, selectable credential kinds and whether an operation is sensitive. Both token and user pickers consume it. A server-side validation method is authoritative; UI selections are only input.

Ordinary users receive explicit capabilities, never the backward-compatible broad `api` wildcard or reserved administration capabilities. “Select all” expands the currently available ordinary scopes; it is not a permanent grant to future API sections. Machine tokens may retain broad `api` where already supported; label that option clearly. Unknown scopes in newly submitted grants are rejected unless contributed by a registered, authoritative module catalog.

Avoid an over-restrictive parent policy: an exact `api.workflows.respond` token must not suddenly also need `api.workflows.read` just because all workflow paths share a group. The ordinary endpoint rule can be `api OR exact required data scope`; the existing privileged endpoint keeps its exact requirement. Keep domain requirements as additional checks. [S07] Permission classification is semantic, not merely `GET=read`: a probe or export can have effects/sensitive content.

The administrative HTTP policy requires both an enabled surface and a **server-validated configured-admin session**. A reserved `api.access.manage` scope can describe the capability, but possession of that claim on a machine token is not enough. The low-level machine issuer cannot mint a user/admin session kind. Any `api.tokens.issue` compatibility behavior must not create an indirect administrative escape.

Enforce administrative authorization in the owning application service via a narrow access interface implemented by the host. Direct UI uses trusted local authority; HTTP uses the verified admin session. No normal request becomes admin because the interactive provider is unavailable. Avoid duplicate ad hoc checks in every Razor callback.

## 3.8 Route completeness and cross-boundary calls

Inventory actual endpoint metadata from the composed host: API route groups, routes mapped directly on `app`, shared-provider aliases, project-structure/runtime paths, event streams, attachments, authorized downloads and OpenAPI. Classify intentionally anonymous status/health/discovery operations narrowly and document them. All other exposed business operations require the correct section decision in secure mode. A route-string prefix alone is not a reliable authorization mechanism.

Add a meaningful coverage test for endpoint metadata, plus representative real requests for every section and sensitive operation. This is a security invariant, not a brittle test that freezes the number of endpoints or partial classes. Endpoint coverage cannot be inferred from a static list that was never compared with the real host.

Keep current subject mapping and existing caller-bound processes/leases/history semantics. API accounts are stable principals, but per-resource ownership is not being redesigned. Do not solve a failing endpoint test by substituting a local operator or broadly disabling its resource policy. For internal HTTP calls, reuse the current controlled credential/caller mechanism; never pass an env-admin session to all agents or providers.

## 3.9 Browser, proxy and operator boundaries

Use same-origin deployment of the API-only UI where possible. If cross-origin development is needed, use ASP.NET Core CORS with exact configured origins and narrowly relevant methods/headers. Bearer authentication does not require a broad credentials-enabled cookie policy. Do not combine arbitrary origins with credentials. CORS is a browser behavior policy, not authorization for direct clients. [S06]

Require encrypted transport for real username/password and bearer traffic, typically HTTPS terminated at the known proxy; a loopback development exception can be explicit. Do not mistake `RequireHttpsMetadata=false` for an HTTP transport policy. Use trusted forwarded headers, not arbitrary client-supplied `X-Forwarded-Proto`, to determine original TLS where required. A private VPN/network can be part of the operator's transport model, but cleartext credentials must not be exposed on an uncontrolled hop.

A server profile must keep the backend port off untrusted interfaces and configure a default-deny proxy allowlist. Block SSR routes, `/settings`, `/_blazor` including negotiate/reconnect transports, `/_dev`, unapproved file routes, and any backend fallback. Allow additional API/download paths only after inventory and authorization review. Blocking `/settings` alone does not hide the interactive administrative UI. Do not use source IP appearing as localhost behind the proxy as evidence of an HTTP administrator.

The HTTP master switches and signing/bootstrap secrets are immutable deployment inputs in v1. Show their effective state in Settings; changing them requires configuration and restart. User records and token metadata remain editable through protected application services. This distinction prevents “administrative API enables itself” and avoids a generic remote configuration editor.

## 3.10 Explicit non-goals and compatibility changes

Non-goals: public registration, email, federation/OIDC server implementation, refresh tokens, multi-admin role hierarchy, multi-tenancy, new per-object ownership, global SSR login, distributed identity storage, automatic key rotation, public-internet certification, EGCP integration, and importing the alternative UI.

Intentional changes: remote token issuance becomes independently opt-in and admin-session-only; limited tokens no longer pass unrelated general API families; new account/scope writes are validated; bad secure configuration fails closed. Preserve existing machine credentials for legitimate permitted operations, existing `api` data semantics where supported, existing exact privileged checks, machine token TTLs and local operator UI administration.

For public or federated exposure later, prefer a standards-based identity provider and supported token flows rather than presenting this closed-instance password login as a general OAuth design. Microsoft explicitly recommends standards-based token acquisition for general production identity scenarios; the proposed bounded local mechanism is a product trade-off for this internal-instance requirement, not a claim of equivalent functionality. [S01]
