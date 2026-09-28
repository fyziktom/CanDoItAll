# API users and deployment access

API users are ordinary accounts with explicit capabilities. The configured administrator
manages accounts and credentials. Settings → API access remains an in-process operator
surface; protect that page and the Blazor connection separately from the HTTP API.
The administrator does not implicitly receive workspace business permissions.

## Configuration

The following settings are deployment-owned. Environment variables use double underscores,
for example `Api__UserAuthentication__Enabled`.

| Setting | Default | Meaning |
| --- | --- | --- |
| `Api:UserAuthentication:Enabled` | `false` | Maps login, current-session and logout operations. |
| `Api:AccessManagement:Enabled` | `false` | Independently maps account and token administration over HTTP. |
| `Api:BootstrapAdmin:UserName` | `admin` | Reserved configured administrator name; never an ordinary account. |
| `Api:BootstrapAdmin:PasswordHash` | empty | Required supported password hash when user authentication is enabled. No default password. |
| `Api:UserAuthentication:TokenLifetimeMinutes` | `60` | Positive session lifetime, no greater than the configured JWT maximum. |
| `Api:UserAuthentication:AllowLoopbackHttp` | `false` | Explicit development exception for direct loopback HTTP. |
| `WebHost:TrustedProxies` | empty array | Exact proxy IP addresses allowed to supply forwarded client address and scheme. |
| `WebHost:AllowedOrigins` | empty array | Exact HTTP/HTTPS browser origins permitted by CORS; no wildcard or credentials. |

The existing JWT issuer, audience and signing key remain in `Api:Authorization`.
The signing key must contain at least 32 UTF-8 bytes. Machine-token defaults remain
480 minutes, with a 1440-minute maximum. Session and machine lifetimes are independent
within that maximum.

| Main API | JWT | User authentication | HTTP management | Result |
| --- | --- | --- | --- | --- |
| off | either | off | off | Main API absent; existing separately mapped runtime, Project Structure and file boundaries retain their own policies. |
| on | off | off | off | Trusted open business API; no user isolation, login or HTTP administration. |
| on | on | off | off | Machine/legacy business API; login and HTTP administration return 404. |
| on | on | on | off | Login/me/logout available; all HTTP management, including token issuance, returns 404. Local Settings remains available to the trusted operator. |
| on | on | on | on | Account and token management requires a registered configured-administrator session. |
| any other combination | | | | Startup fails explicitly. |

Enabling user authentication also requires a valid configured administrator hash, valid
username and valid lifetime. Missing or malformed configuration never creates an open
fallback. `/api/access/status` reports effective switches and lifetimes without secrets.

## Generate and install the administrator hash

Build and run the local helper from the repository root:

```powershell
dotnet build tools/ApiAccess/ApiPasswordHash/ApiPasswordHash.csproj --configuration Release
dotnet tools/ApiAccess/ApiPasswordHash/bin/Release/net10.0/ApiPasswordHash.dll
```

Enter a developer/operator-owned password at the hidden prompt. The helper accepts no
command-line password argument. A trusted provisioning process can instead provide one
line on standard input; never place that line in shell history, tracked fixtures or logs.
Only the encoded hash is written to standard output. Install it as
`Api__BootstrapAdmin__PasswordHash` using the deployment's private configuration mechanism,
along with the separate JWT signing key. Keep the hash private as well.

The helper and server share ASP.NET Core Identity V3 password hashing: PBKDF2-HMAC-SHA512,
220,000 iterations and a random salt. Login can upgrade supported older ordinary-account
hashes. Configured administrator hashes must already meet the current work factor.
Passwords are 12–256 characters and are never trimmed. Usernames are 1–64 ASCII letters,
digits or `._-@`, compared case-insensitively through invariant uppercase normalization;
leading/trailing spaces are invalid. Display names are 1–128 characters, trimmed and
cannot contain control characters.

Generate a new hash and restart to rotate the administrator password. Existing
administrator sessions become invalid; ordinary accounts and machine tokens remain valid
when the signing key is unchanged. Rotating the signing key invalidates all signed tokens.

## Client operations and capabilities

`POST /api/access/login` accepts only `userName` and `password` and returns a fresh
registered bearer token, expiry and safe session identity. `GET /api/access/me` reads that
identity; `POST /api/access/logout` revokes just the current session and returns 204.
There are no refresh tokens: authenticate again after expiry. Do not persist passwords in
browser storage. Token-bearing responses and access operations use `Cache-Control: no-store`.

Send tokens only in the `Authorization: Bearer ...` header, including for event streams;
query-string tokens are rejected. Keep credentials out of URLs and request logs. Production
defaults keep ASP.NET Core request logging at `Warning`; retain that setting or redact
sensitive query values before enabling informational request-URL logging.

API failures use safe JSON while preserving explicit response statuses. Unexpected faults
return 500; unavailable access infrastructure returns 503. Neither response exposes
exception messages, stack traces or credential material.

HTTP administrators can list/create users at `/api/access/users`, read/update/delete
`/api/access/users/{id}`, and reset passwords at its `/reset-password` child. Updates and
password resets carry the current `expectedVersion`; deletion supplies it in the query.
Stale writes and duplicate usernames return 409. List paging defaults to 25, maximum 100.
Create/update requests select only catalog capabilities; unknown or reserved grants and
unknown JSON properties are rejected. User DTOs never expose hashes or credential bindings.

Changing a profile, permissions, password or enabled state invalidates that user's existing
sessions on the next validation. Re-enabling does not revive old sessions. Deleting and
recreating the same username creates a different GUID. These operations do not change
machine credentials.

The authoritative capability catalog is
[`ApiScopeCatalog`](../src/Modules/CanDoItAll.Modules.Workspace/ApiAccess/ApiScopeCatalog.cs)
and `GET /api/access/scopes` for administrators. Settings groups the same catalog by section
and marks sensitive grants. Ordinary users cannot select broad `api`, the legacy issuance
label, self-session internals or access-administrator authority. An empty business selection
permits only the account's self-session operations. Write and execute capabilities do not
implicitly grant read capabilities; select each required capability.

The legacy `agent-recruiting.review` capability remains assignable to users and machine
credentials. An exact review-only token can append a recruiting decision without agent
read/write grants; broad `api` never substitutes for that review capability or activates an agent.

Section policies cover projects, agents, workflows, processes, prompts, CRM/HR, plugins,
workspace settings and runtime. Existing Memory, Simple Chat, Project Structure and shared
provider capabilities retain their own routes. Existing exact workflow-response,
provider-history and storage-recovery policies, publication grants and domain ownership
checks remain additional restrictions. A signed shared-provider token cannot access other
sections merely because its signature is valid.

`/api/access/tokens` lists metadata or issues a machine token. Listing defaults to machine
credentials; `kind=UserSession` or `kind=AdministratorSession` selects session metadata.
`POST /api/access/tokens/{id}/revoke` and `DELETE /api/access/tokens/{id}` invalidate that
registration. The token plaintext is returned only at issuance. A bearer containing broad
`api`, old `api.tokens.issue`, an administrator-looking subject or a claimed role never
establishes administrative authority. HTTP requests from loopback get no local UI privilege.

## Added business API operations

| Operations | Authority and behavior |
| --- | --- |
| `GET /api/processes/definitions` and `/{definitionKey}`, `/{definitionKey}/roles`, `/{definitionKey}/steps` | Process read capability. Current catalog/editor projections; keys are opaque strings. Optional search is bounded to 256 characters and scope filters must be valid enum values. Missing definitions return 404; infrastructure failures remain service errors. |
| `GET /api/workflows/templates` | Workflow read capability. Template metadata, counts, preferred backend and a display-only flow summary. |
| `POST /api/workflows/templates/{templateKey}/drafts` | Workflow write capability. Uses the same application owner as Settings' workflow template action. Creates a draft and its component, never executes a model. |
| `GET /api/settings/workspace`, `PUT /api/settings/workspace` | Separate workspace-settings read/write capabilities under the main API switch. Only workspace name, default provider, output format, currency/culture and notes. |

Template draft creation requires an enabled structured-output provider with an available
configured default model. It validates before writing and compensates an uncommitted
definition write by deleting only its own component. External-call approval stays required.
Each request creates new GUIDs; retries can create another draft. Friendly names use the
next available suffix; simultaneous requests may share a friendly name but cannot overwrite
each other's identities. An uncertain cleanup produces an explicit operator-review error.

Workspace settings return persisted values after a write. If the write commits but its
read-back fails, the response remains successful with the saved snapshot and
`X-CanDoItAll-Read-Back: pending`; refresh the read, rather than resubmitting the write.
These settings never expose or mutate API users, keys, hashes or exposure switches.
The generated OpenAPI document remains authoritative for JSON shapes and numeric enums.

## Private persistence and deployment boundary

Accounts live in `api-users/accounts.json` under the private control-plane root; token
registration uses the existing private registry there. Both survive a workspace database
profile change. The account document uses atomic durable replacement and coordinated
mutations, with version and uniqueness checks inside that boundary. Unsupported schemas
and corrupt documents fail closed instead of being overwritten with an empty store.
Back up the control-plane root and private deployment configuration together, preserving
their filesystem permissions. Protect backups as credential material.

Managed v1 machine tokens and deliberately unmarked legacy tokens retain their compatible
data path, including broad `api` where historically supported. Managed v2 sessions require
current registry metadata and account revision or current administrator-credential binding.
Unknown managed versions never fall through to legacy acceptance. Older binaries do not
understand v2 sessions: disable the new surfaces and require new login after an upgrade;
retain a protected backup before rolling back private schema changes.

When user authentication is on, HTTPS is required for API and authorized-file traffic.
The opt-in loopback HTTP exception requires original and effective loopback peers and no
forwarding headers. It is not a proxy deployment setting. JWT lifetime validation retains
the existing 30-second clock skew; managed session state also enforces its registered expiry.
Event streams recheck current credentials before sensitive frames and at most every
15 seconds while idle, so expiry, account changes and registry revocation close a live stream.

Terminate TLS at a proxy that trusts only the configured upstream network. Bind the backend
to loopback (same-host proxy), or a private container network with no published backend port.
Configure `WebHost:TrustedProxies` with exact proxy addresses and allow only intended paths.
A representative same-host nginx routing policy uses its documented
[proxy directives](https://nginx.org/en/docs/http/ngx_http_proxy_module.html):

```nginx
location /api/ {
    proxy_pass http://127.0.0.1:5032;
    proxy_set_header Host $host;
    proxy_set_header X-Forwarded-For $remote_addr;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_buffering off;
}
location / {
    return 404;
}
```

Install this inside the operator's HTTPS server configuration. Its default deny excludes
SSR, `/settings`, `/_blazor`, `/_dev`, Swagger/OpenAPI and file routes; explicitly review any
additional path before exposing it. Configure exact CORS origins only if a browser client
uses another origin. CORS does not authenticate direct clients. Production validation uses
an equivalent real TLS YARP proxy and verifies that the backend listeners stay on loopback.

This is a single-host internal PC/LAN/VPN account model. It adds no SSO, registration,
recovery email, account promotion or application-wide SSR identity system.
