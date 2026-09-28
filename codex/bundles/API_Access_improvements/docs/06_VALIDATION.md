# 6. Validation and completion evidence

**Initial status of every application scenario below: NOT RUN.** These are acceptance criteria for Codex, not results from preparing this package.

## 6.1 Test selection without repeatedly running everything

The current repository's `docs/testing.md` explicitly separates the product build graph from Unit, Components, Integration, Memory and Playwright solutions. Its local loop requires changed production builds, a bounded owning filter, expected and actual discovery counts, and fresh test assemblies before `--no-build --no-restore`. Its static portability gate is mandatory for these edits. [D16, D17]

Use the current SharedInfo `candoitall-codeanalytics-mcp` skill. The canonical impacted-test capability is `code_analytics_impacted_tests_get`, a live-workspace query that does not require an architecture snapshot. [D18]

Request fields documented in that skill are `repositoryRootPath`, `testWorkspaces`, `changes`, `contextOnlyPaths`, `maxVisitedMembers`, `maxSelectors` and `maxReasonPaths`. Each actual change can include one-based inclusive `lineRanges` and `behaviorIntent`. Supply the separate relevant test solutions; the product `.slnx` does not magically include them.

For this task, authorization/DTO/mapping changes are `BehaviorChange` or `ContractOrShapeChange`; start conservatively with `Unknown` when unsure. Do not claim behavior-preserving intent merely because a method signature did not change. Read-only inspected files belong under `contextOnlyPaths`, not in the change set.

Validate workspace health and source-test discovery. Run all required selectors; evaluate conditional promotion triggers after expanding the diff, changing DI/serialization, discovering dynamic calls, or seeing an owning failure. `AllSuppliedSuites` is an instruction to run those supplied workspaces, not permission to skip because the selector list is empty. A broad impacted result can legitimately happen for authentication middleware. Record why a broader final run is necessary rather than repeating it after each small edit.

MCP output is test-selection evidence, not execution evidence. Use it to avoid irrelevant repetition, not to eliminate the explicit negative-security scenarios below. A static call graph may miss endpoint conventions, Razor lifecycle behavior, serialization and generated clients.

## 6.2 Concrete starting points

The reviewed `tests/Integration/CanDoItAll.Tests.Integration/ApiAccessAuthorizationIntegrationTests.cs` contains these relevant behaviors: [D12]

- `TOKEN_SCOPES_empty_selection_never_grants_broad_api_access`;
- `API_BOUNDARY_local_operator_ui_identity_does_not_authenticate_http_boundaries`;
- `Token_issuance_requires_explicit_privileged_scope`;
- `Authorization_disabled_token_endpoint_is_not_protected_by_the_scope_policy`;
- `Project_structure_routes_require_write_scope_and_bind_lease_owner_to_token_subject`;
- `TOKEN_LIFECYCLE_deleted_and_revoked_tokens_fail_real_http_requests`;
- `TOKEN_LIFECYCLE_corrupt_registration_fails_closed_on_http`.

The two existing issuance tests encode behavior intentionally changed by this task. Replace/extend their assertions with the new gate/admin truth table and record why; do not delete negative coverage to get green results.

Additional discovery seeds include `ApiTokenRegistryTests`, `ApiTokenAdministrationTests`, `ApiTestHost`, current shared-provider integration tests, current principal/history/authorized-file tests and Settings/Playwright tests. Some names were located by default-branch search; resolve the actual development symbols through CodeAnalytics before relying on them. Do not invent browser class names or expected discovery counts.

A bounded starting command, to be adjusted after the actual impact result, is:

```powershell
$tests = './tests/Solutions/CanDoItAll.Tests.Integration.slnx'
$filter = 'FullyQualifiedName~CanDoItAll.Tests.Integration.Api.ApiAccessAuthorizationIntegrationTests'

dotnet test $tests --configuration Release --list-tests --filter $filter /m:1
# Confirm the expected non-zero discovery count and that the assembly is current.
dotnet test $tests --configuration Release --no-build --no-restore --filter $filter /m:1
```

Build every changed production project directly and refresh the owning test graph first. Determine additional Unit/Components/Playwright filters from actual edits. Run `portability-static` exactly as currently documented, review genuine findings and intentional baseline changes, then finish with enforcement without baseline writing. No source-shape or class-count test should substitute for behavioral proof.

## 6.3 Proof layers

**Unit:** deterministic grant validation, user normalization, hash verification/error paths, account/token serialization compatibility, revisions, configuration and admin-policy decisions.

**Focused HTTP integration:** real JWT Bearer handler, registration store and endpoint policies using the existing API harness. The documented test-only InMemory database can support fast cases; do not replace authentication with a fake “always admin” scheme for these proofs.

**Production composition:** start the actual host with isolated control-plane files and supported database configuration. Use real HTTP requests over a bound loopback port, including the production middleware/error pipeline. This catches route mapping/configuration differences from the test harness. Final persisted business-endpoint/UI proof uses the supported production database; a blocked PostgreSQL/container lane remains blocked, not “passed via InMemory.”

**Browser:** Playwright against the shipped Settings page, performing actual account mutations and verifying subsequent API behavior. Isolated local/synthetic provider fixtures are enough; no real paid model requests are required for access-control proof.

**Deployment boundary:** a representative configured reverse proxy or the actual deployment routing test, proving SSR/interactive/dev endpoints are hidden and forwarding cannot create admin authority. A unit test of an IP helper alone is not this proof.

## 6.4 Acceptance matrix

Use valid request bodies and existing fixture objects for forbidden operations, so a 400/404 from malformed input is not mistaken for successful authorization denial. For denied writes, assert the account store, registry or relevant business state did not change. Disable automatic HTTP redirects while checking API statuses.

### A — Configuration and exposure

| ID | Scenario | Required result |
|---|---|---|
| A01 | Existing trusted open mode; all new flags off. | Ordinary open behavior preserved; all new login/admin routes and old HTTP token issuance absent/404. |
| A02 | JWT on, user auth/admin off. | Pre-existing machine token works for allowed data; no user-login/admin exposure. |
| A03 | User auth on, HTTP management off. | Login/me/logout work; every management route is 404 even with a valid admin session. |
| A04 | Management on with secure prerequisites. | Admin succeeds, no bearer is 401, ordinary user/machine is 403. |
| A05 | Main API off and new flags off. | New surfaces absent; existing exceptions match the documented contract, not a false global kill-switch assertion. |
| A06 | Every invalid flag combination, missing key/hash, malformed hash, invalid lifetimes. | Explicit startup/configuration failure; no open fallback; no secrets logged. |
| A07 | Generated OpenAPI under management off/on. | Disabled routes not advertised as callable; unique operation IDs, correct bearer/login metadata and no secrets. |
| A08 | Production error pipeline: absent route, denied request and service failure. | Status retained, safe API content, no 200 HTML fallback or unwanted login redirect. |

### B — Login, identity and credentials

| ID | Scenario | Required result |
|---|---|---|
| B01 | Configured administrator authenticates with real hasher and handler. | Registered admin session; no arbitrary role creation path. |
| B02 | Admin-created ordinary user authenticates and calls `me`. | Stable user ID and exactly allowed effective scopes; machine credential is not treated as that user. |
| B03 | Wrong password, unknown name, disabled user. | Same safe invalid-credentials response; comparable hash work, no account inventory disclosure. |
| B04 | Null/oversize inputs; role/sub/scopes/hash/kind/revision overposting. | Bounded validation rejection, no privilege escalation or secret reflection. |
| B05 | Username casing/normalization and duplicate concurrent creation. | Documented case behavior; one canonical identity, no duplicate or admin-name collision. |
| B06 | Wrong issuer/audience/signature/algorithm, unsigned token, expiry/future validity, invalid/duplicate critical claims. | Authentication denied; registry/kind rules cannot be bypassed. Test through real handler. |
| B07 | A machine JWT has admin-looking subject/name/role and reserved admin scope. | Still not an administrator; cannot use management routes or user-self operations. |
| B08 | Throttling with rotating usernames and untrusted forwarding headers. | Bounded limiter state/work, safe 429/retry behavior; no permanent attacker-triggered account lockout. |
| B09 | Login/token response, DTO serialization, logs and OpenAPI. | No-store on sensitive responses; no password hash/key/raw secret leakage. |
| B10 | Token expires and the user logs in again. | New registered JWT acquired without administrator issuance; expired token remains invalid. No stored browser password. |

### C — Account changes and revocation

| ID | Scenario | Required result |
|---|---|---|
| C01 | Remove/change a user's scopes while its JWT is active. | Old JWT rejected on the next validation; new login reflects the new set. |
| C02 | Reset password. | Old password rejected, old user sessions invalidated, new password works; machine tokens unaffected. |
| C03 | Disable, then re-enable. | Disabled login/session fails; re-enable permits new login but does not resurrect old JWTs. |
| C04 | Delete and recreate the same username. | New immutable ID; all deleted user's sessions remain invalid. |
| C05 | Logout one of two sessions. | Current token rejected; other session works until separately revoked/expired. |
| C06 | Concurrent profile update/password reset/login. | No stale write restores hash/permissions/revision; no usable superseded grants. |
| C07 | Configured admin hash rotation and restart with unchanged signing key. | Old admin session fails; new password works; legitimate machine token remains valid. |
| C08 | Registry/user-store corruption, missing user and unsupported schema. | Fail closed, safe diagnostic; no empty-store overwrite or fallback administrator. |
| C09 | Controlled clock around expiry and existing clock skew. | Behavior matches the documented policy; tests do not rely on flaky multi-minute sleeps. |

### D — Permission enforcement and legacy compatibility

| ID | Scenario | Required result |
|---|---|---|
| D01 | For each API section, a narrowly scoped user makes an allowed operation. | Correct positive response with expected safe state/body. |
| D02 | For each section, that user calls representative unrelated read/write/execute operations. | 403 before handler effects; no cross-section access from a merely valid JWT. |
| D03 | User has no business grants. | No implicit `api`; only explicitly defined self-session capability, or the chosen documented empty-grant behavior. |
| D04 | Existing broad `api` machine token. | Ordinary compatibility access works where historically intended; no new admin or exact privileged bypass. |
| D05 | Existing exact-only memory/chat/project-structure/shared-provider token. | Correct existing capabilities remain usable without requiring unrelated parent scopes. |
| D06 | Existing response/history/recovery privileged scopes and domain ownership. | Exact privileges and resource checks remain enforced; ordinary broad data access does not bypass them. |
| D07 | Legacy unmarked valid JWT and registered v1 JWT. | Deliberate compatible data behavior; neither gets typed admin authority. |
| D08 | Registered machine revoke/delete/corrupt record. | Existing real-HTTP rejection maintained. Unknown new session versions never fall through to legacy acceptance. |
| D09 | Real shared-provider catalog/inference client path with permitted local fixture. | Correct authorized operation and existing publication checks; no regression to client-instance use. |
| D10 | Endpoint-data-source inventory including direct routes, aliases, runtime/project-structure, files and streams. | No unclassified protected data operation; anonymous exceptions are explicit and justified. |
| D11 | Active event stream after expiry, user reset/disable/scope edit or token revoke. | Stream closes within the documented bounded check interval; no further recognized-forbidden events. |
| D12 | Client-supplied agent/owner/role headers and internal service calls. | Existing caller binding holds; no ambient local/admin credential supplied to ordinary execution. |

### E — Colleague endpoint integration

| ID | Scenario | Required result |
|---|---|---|
| E01 | Four process-definition reads, real fixture keys and valid filters. | Correct catalog/editor/roles/steps shapes and grants; bad key vs infrastructure failure distinguished. |
| E02 | Workflow template listing. | Actual template metadata and display summary; no accidental execution. |
| E03 | Template draft creation. | Stored draft/component consistent, correct lifecycle and existing permissions, no external model call. |
| E04 | Missing template/provider, second-write failure, duplicate/concurrent request. | Documented safe errors/semantics; no orphan or overwrite and no approval downgrade. |
| E05 | Workspace settings GET/PUT and authoritative read-back. | Allowed user succeeds; read-only/other-section user cannot mutate; safe validation and actual persisted values. |
| E06 | The eight added routes under API/auth flag combinations. | Correct gates and scopes; settings cannot escape the parent group by being mapped on `app`. |
| E07 | Generated OpenAPI compared with actual success/error JSON for changed contracts. | Correct DTOs/acknowledgements and no duplicate operations or stale contract lists. |

### F — UI, persistence and deployment

| ID | Scenario | Required result |
|---|---|---|
| F01 | Trusted local Settings with secure JWT on and management HTTP off. | No global SSR login redirect; local token/user controls work with explicit local authority. |
| F02 | Browser creates/edits/resets/disables/re-enables/deletes a user. | Actual writes and HTTP consequences verified after every action, including stale JWT rejection. |
| F03 | Browser scope picker, validation, concurrency, repeated save and committed-read-back failure. | No hidden broad grant, duplicate mutation or stale overwrite; accurate success/error state. |
| F04 | Browser existing machine-token issue/list/revoke. | One-time secret handling and registry lifecycle preserved. |
| F05 | Application restart using same private root and selected database-profile change. | Users, permissions and revocations persist independently of workspace database selection. |
| F06 | Real private-store filesystem on Windows/Linux or named unavailable lane. | Correct persistence/locking behavior and no plaintext passwords; permissions follow existing durable writer conventions. |
| F07 | Direct anonymous HTTP, loopback HTTP, forwarded request and untrusted interactive connection. | No HTTP local-operator elevation; trusted circuit logic not widened. |
| F08 | Representative proxy allows intended API only. | SSR, Settings, interactive transport, developer endpoints and unapproved file routes blocked; direct backend port not an escape. |
| F09 | Allowed/disallowed browser origins and CORS preflight. | Only configured cross-origin flows work; direct-client authorization remains independent. |
| F10 | Final source scan and targeted/broader closure. | Mandatory portability enforcement passes; actual test discovery/results and any broader trigger are recorded. |

## 6.5 Practical end-to-end acceptance journey

Start a clean isolated server profile with secure JWT, user login and HTTP management enabled. Generate secrets locally using the real helper; do not check them into the fixture. Capture only nonsecret configuration in the report.

Log in as the configured administrator. Create a user with, for example, Simple Chats read and workflow read. Authenticate that user through the real endpoint, prove allowed reads, and reject a project write, a workflow mutation and every management operation. Use existing fixtures so denial is not a disguised missing-resource error.

From the real Settings page remove workflow access. Immediately attempt the old bearer again, log in again and verify only the retained capability. Reset the password, disable/re-enable, then delete/recreate the username, checking old and new tokens at every step. Keep an independent pre-existing machine shared-provider credential alive and show that these user changes never revoke it.

Restart the host, verify persistence and admin-hash rotation semantics, then repeat the management-disabled mode to prove local Settings still works while management HTTP is 404. Finish with actual proxy isolation and correct error responses. Use small fixtures and no paid remote model calls.

## 6.6 Evidence format and completion rule

For each test lane record source HEAD, environment, changed production builds, exact commands, expected/actual discovery counts, pass/fail/skip counts, duration, CodeAnalytics required/conditional reasoning, and the redacted evidence location. For live HTTP record scenario ID, method/path, credential **kind**, status, expected/actual effect and safe response assertions, not the bearer/password.

For UI record actions performed, persisted state checked and linked HTTP assertions. Screenshot only safe UI states. A static screenshot of a form is not proof that its handlers work. A runner that “passes” after returning early from a disabled fixture gate is not live proof.

Use the provided `examples/validation-evidence.template.json` as a starting shape. Its initial `not_run` values must not be changed without execution. Keep planned, passed, failed, blocked, skipped and not-run evidence separate.

The implementation is ready for review only when required gates and actual API/UI behaviors pass. A real unavailable dependency can leave a clearly named lane blocked; that is an honest partial implementation, not validated merge readiness. Do not perform the actual development-to-main merge as part of this task.
