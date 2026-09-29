# Execute: Workspace API Access UI decoupling

You are Codex GPT-6 Astra Max acting as the senior C#/.NET and Blazor engineer for CanDoItAll.
Implement this assignment, not just a plan. First close the bounded Files carry-over WSC-R1,
then complete the existing API Access Settings section through a lightweight renderer and
its own faithful sandbox. Do not stop after the correction and do not start a third slice.

This is an incremental development-loop refactor. It is not API-only UI, an authentication
redesign, a new admin system, or permission to reorganize every Workspace responsibility.
Reason independently, choose small cohesive boundaries and improve the proposed design where
current code gives stronger evidence. Preserve all safety and behavior requirements below.
There are no interface/class-count quotas or prescribed subbundle checkpoints.

## 1. Establish the actual baseline

The review used branch `components-decoupling`, implementation
`186ab60a8e1a2dd7ab79c71e1a256cb0b2fdaedf` (Workspace Settings Core and Resources readiness).
Its preceding commit archives the previous handoff. This SHA identifies reviewed evidence;
**do not checkout/reset to it**. Work on the current owner-assigned branch and record actual
HEAD, dirty state and sibling revisions. Distinguish archival instructions from product edits.
The user's repeated Memory wording does not require re-extracting Memory.

Before source/build/test changes, read current `AGENTS.md`, `.github/copilot-instructions.md`,
`docs/architecture/ui-component-seams.md`, `docs/testing.md`, current CI and the applicable
CanDoItAll.SharedInfo standards. Read this complete package:
[Core review](CORE_REVIEW.md), [selection](MODULE_SELECTION.md), [architecture](ARCHITECTURE.md),
[API source notes](API_ACCESS_REVIEW_NOTES.md), [sensitive state](SENSITIVE_STATE.md),
[validation matrix](VALIDATION_MATRIX.md), [development loop](DEV_LOOP.md),
[proof limits](PROOF_STATUS.md) and [shared foundation](shared/README.md).
The [source register](SOURCES.md) states exact inspected coverage, not a claim that every
consumer has already been audited. Historical shared maps are not today's queue.

Use CodeAnalytics and Components MCP when actually available to identify consumers, affected
tests and existing component contracts. Try the real tools; if unavailable, use local source,
project evaluation and CLI alternatives, recording the limitation. Do not invent MCP results.
Do not replace existing components with homemade substitutes solely to make a sandbox build.

## 2. S0 — narrow correction and closure of Core

Reproduce **WSC-R1** from CORE_REVIEW.md before changing its behavior:
an existing file preference is selected, a different destination extension is entered,
Save is held, and the same draft's executable path is edited before its known completion.
The current code refuses to adopt the saved destination because the whole draft revision
changed; Use system default can remain attached to the previous extension.

Separate destination-identity acceptance from field reconciliation. If the same editor
still targets the captured extension, keep its confirmed identity even when later path
text must survive. A newer target/selection/New/disposed draft must not inherit old identity.
Preserve extension edit-away-and-back semantics, form context, known-warning outcomes and
unknown locks. Do not rename/delete the old extension while saving a new destination.
Test the actual immediate-input form and exact next Delete target, not just direct assignment.
Use the existing Files owner; no new storage protocol or Core→API dependency is needed.

Preserve Resources RS-R1: catalog readiness is not exact editor acquisition, same-ID failed
loads can retry, and handler admission rejects placeholders. Preserve Memory origin fixes,
SecretField retirement, secret metadata-only observations and history policy authority.
Run the narrowly affected current Core tests and relevant production path. If checkout drift
already fixes WSC-R1, demonstrate the regression passes and do not rewrite working code.
Only genuinely demonstrated adjacent blockers justify extra S0 changes; record their scope.
Continue into API Access once that bounded prerequisite is established.

## 3. A1 — inventory, contracts and original baseline

Map the entire existing API Access tree and actual consumers before moving it:
`WorkspaceApiAccessHost`, token issuance, token metadata dialog, scope picker, ordinary-user
administration and all nested confirmations. Include status unavailable, authorization off,
key missing and granted/denied variants. Preserve `/settings?tab=api-access` and the eight
Settings navigation entries. Providers remains `/agents?tab=providers` replacement navigation.

Trace the concrete services, Web administration-access adapter, issuer, scope catalog,
user/token stores, HTTP endpoints/filters, current component tests and session/permission
consumers. Record which classes are mixed value/runtime files and which data are sensitive.
Do not move private runtime records just because a renderer needs a safe projection.

Capture the original real feature screenshots/interaction baseline and Web development loop
before extraction. Record the existing Core sandbox closure separately; it is the protected
small loop. Use a task-owned configuration/port/control-plane root, never the ordinary app.
No executable launch, real credential issuance for the user's account or live external provider
is needed. The baseline must distinguish synthetic fixture effects from actual owner behavior.

## 4. A2 — isolate the API leaf without growing general Workspace

Recommended projects:

```text
src/Modules/CanDoItAll.Modules.Workspace.ApiAccess.Contracts
src/UI/CanDoItAll.Workspace.ApiAccess.UI
src/Sandboxes/CanDoItAll.Workspace.ApiAccess.UiSandbox
```

Use equivalent already-existing light projects when justified. A shared API presentation
project is optional, only for actual shared state/effect logic. Keep separate cohesive
responsibilities for issuance, token list/confirmation and user editor/list; do not create
one universal settings manager or service bag. An interface needs a real substitution or
ownership boundary, not a quota.

The generic Core UI/contracts/presentation/sandbox must not acquire the API leaf or its
runtime dependencies. Compose the leaf from the production module route through the existing
active slot. The API sandbox mounts the same feature renderers over scenario owners; it
need not import all Core for navigation. Data Sources and Storage hosts remain untouched.

Use safe UI-specific projections where they eliminate a real implementation edge. Keep
canonical scope vocabulary/validation in one actual owner; project available definitions,
kind restrictions and effective status rather than duplicating authorization policy. Do not
ship a hard-coded second catalog with the same scopes. Reusing canonical pure types is fine
only after checking their real transitive graph and owner placement.

**Forbidden shortcuts:** UI/sandbox references to Workspace implementation, Infrastructure,
JWT/password runtime, production DI, a service locator, a delegate bag with all services,
or a Core reference added only for receipt reuse. No Infrastructure→product.Contracts
reverse edge; no expansion of broad Security.Abstractions into an API administration framework.
Preserve namespaces where useful without confusing namespace declarations with dependencies.

The leaf excludes signing keys, password hashes, bootstrap credential configuration, private
credential bindings and full token records. It receives only necessary safe data and narrow
ports. Production owns authorization and storage. UI disablement is not enforcement.

## 5. A3 — implement the complete behavior safely

### Captured requests and independent state

Capture complete submission inputs before the first incomplete await, including scopes and
all mutable collections. Keep raw incomplete lifetime/field text and form validation until
explicit correction; do not coerce invalid input to a valid default. Bind immediate text
input before blur. A late permission result cannot assemble an issuance request from old
scopes and new subject/lifetime fields.

Keep desired query text, accepted query/page, current editor/overlay and admitted operation
as separate state. Fence results, errors and completion using the same origin checks.
Cover read A→B→A, late failures, initial errors, missing selected records, explicit retries,
parent/child close/reopen and disposal. Do not let a stale read clear a successor's busy flag
or replace unavailable state with a false empty catalog.

Operation gates belong inside handlers. Repeated Save/Enter/click/revoke/delete cannot dispatch
a second conflicting operation merely because a button or stale renderer says it is enabled.
Independent harmless reads may proceed, but they cannot retire or unlock a pending mutation.
Record bounded safe outcomes without evicting unresolved ones to make room for another write.

### Machine token issuance and one-time value

Keep current machine scopes/lifetime validation and the one-time bearer UX. Capture one
issuance intent and return its outcome once to the original authorized editor. Preserve
newer draft fields for the next deliberate issuance; no automatic second issuance after
refresh/read failure. The issuer/registry remain the real owners of signing and registration.

Keep the bearer out of receipt history, ordinary metadata, errors, URLs, storage and inactive
DOM. Retire disclosure on owner view/caller invalidation; release sensitive references.
A cancelled view cannot undo an already registered token. Do not automatically revoke it,
re-sign it, recover it from metadata or parse it in UI to infer permission. Exact read-only
registration inspection and an explicit separately authorized revoke are distinct actions.
If a candidate ID is exposed for an unknown registration, label it as a candidate, not commit.

### Token list, paging and dialogs

Preserve lazy opening, search, 25-item UI paging, refresh, revocation status, expiry and delete.
Keep the default Machine filter; do not silently change it to all sessions or build a new
credential-kind management feature. Preserve current status precedence and owner limits.

Use captured request identities. Retained rows on read failure must identify their stale
query; bounded page correction after deletion must not hijack a newer query. Confirmation
carries exact ID/kind/action and its parent's lifetime. Cancelling a scope/confirmation dialog
must not mutate its editor. Parent close retires only its children, never all global dialogs.

### Account editor and password reset

Preserve create, versioned edit, enabled state, explicit business grants, password reset and
delete. No ordinary account may create/promote an administrator or use the configured admin
name. Empty user grants stay valid; no compatibility grant is added implicitly. Preserve
password length/no-trim rules, hashing, username normalization/uniqueness, expected-version
conflicts, immutable IDs and authentication-revision invalidation at the owner.

After known writes, retain safe returned ID/version before list refresh. A lost list response
must never become an unacknowledged create. Preserve later editor fields without lending
another user/version the old result. Do not weaken the existing held-create/read-back test.
Passwords stay only in the active create/reset editor and necessary private call; clear
references when retired/completed. No password-bearing commands in receipts or recorder lists.

### Known versus unknown owner effects

The user service currently logs after the store returns. Reproduce a logging fault after
actual durable create/update/reset/delete, then preserve the safe committed outcome at the
real owner boundary. A narrow internal outcome or safe diagnostic isolation is allowed.
Likewise distinguish acknowledged token registration from a genuinely ambiguous writer result.
Do not invent success by querying a username or equating generated ID with persisted ID.

Keep known refusal, confirmed result, committed secondary warning and genuinely unknown result
separate. Never turn known success into Unknown because observation or notification failed.
Read-only recovery does not prove the original request completed. Unknown creates must not be
replayed automatically. Do not add a durable command queue, automatic retry protocol or crypto
changes. Any small owner adjustment must preserve affected HTTP schemas/status semantics and
be tested through the real endpoints; update the relevant adapter rather than leaking internal
exceptions into generic 400/500 handling.

### Access and configuration boundaries

Use the real administration access checker for every owner action. Preserve trusted local
operator versus validated configured-administrator HTTP session, access-management feature
gate, ordinary/session/machine distinction and server registration checks. Neither broad
`api` nor the legacy `api.tokens.issue` label becomes administrator authority.

API users/tokens are instance-local control-plane data, not selected database data. Pin the
correct instance and current authorized caller/view epoch. A database switch must not migrate,
recreate or erase these records. Caller/access retirement cancels reads, blocks new dispatch
and discards sensitive presentation; late admitted results remain only as permitted original
safe facts. Do not add authorization to all Blazor SSR or change proxy/HTTP topology.

Project safe configured-admin name/status flags rather than injecting the full options tree
into UI. Keep unknown API status distinct from Open/JWT, including a failed activation after
an earlier successful badge. Do not eagerly read API status, account lists or token lists
from unrelated Settings sections. Owner status and management availability may fail separately.

## 6. A4 — representative backend-free API sandbox

Use the same real renderers and shared presentation as production with controlled owner ports.
Scenarios must mutate their own bounded account/token metadata, implement versions and explicit
fault outcomes, and offer held access/read/write/read-back completion. Synthetic issuance
must not sign usable tokens or emulate a working authentication server. Label fixture values
and limits; never claim sandbox security proves production enforcement.

Cover loading, denied, unavailable, empty, representative, multi-page, expired/revoked token,
missing/stale account, validation/conflict, known postcommit warning and unknown result.
Provide actual nested scope/token/user dialogs, one-time disclosure, retired-view completion
and recovery. Reset retires old state; accepted operations affect their original scenario
store rather than being silently dropped or written into the successor. Bound retained stores.

No production DI, DB, vault, filesystem account registry, actual JWT signing or HTTP auth runtime
is required to render it. Use actual BaseLib inputs/tables/dialogs and published static assets.
Copy no fonts into the handoff. Follow established theme generation/content-link patterns;
a content CSS link is not permission to add a Web ProjectReference.

## 7. A5 — validation and measurement

Execute [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md), selecting current tests from the actual
consumer graph. State expected discovery, run build-backed discovery and record actual case
expansions before executing each changed filter. Never equate zero/filtered-away/skipped tests
with success. Refresh assemblies before `--no-build --no-restore`. Avoid simultaneous writes
to the same build configuration/output trees.

Prove separately: deterministic controller logic, real renderer interactions, production
owner durability/authorization, real Settings host/browser, independent published sandbox,
Core sandbox dependency non-regression, and evaluated/runtime/public-type boundaries.

Owner tests use private control-plane storage and disposable real HTTP credentials, not a
fake “allow all” backend. Browser tests must include production status/issuance/token
confirmation/user paths and separate sandbox lifecycle scenarios. Assert issued-token
usability then revocation/deletion denial and session invalidation with actual middleware in
an isolated secured host. Trusted-local UI tests and secured HTTP tests are different proof
layers. Do not log their bearer/passwords or retain revealed screenshots.

Exercise the deferred Data Sources/Storage hosts without changing their configuration and
preserve Core sections/Providers navigation. Only load data appropriate to the active section;
list typing is not permission to issue more commands. Measure original/final API development
loops and the existing Core graph according to [DEV_LOOP.md](DEV_LOOP.md). Do not promise a
full-Web speedup; report project/watch/asset differences and actual sample conditions.

Run current mandatory portability-static review/repair/enforcement, documentation/evidence
checks and secret scanning. A baseline refresh requires reviewed intentional findings and
final no-write enforcement. Assess named broad-Stable triggers after concrete owner/public
changes are known; do not repeatedly run the entire suite merely to finish a phase. The
previous mixed Stable run plus focused repair is not a clean all-green run to reuse blindly.

## 8. Working discipline and completion

Preserve the ordinary application on port 5032, its database, credentials, storage and installed
resources. Provision only task-owned fixtures; label and verify exact containers/processes/
roots before cleanup. Do not kill arbitrary dotnet processes or clean unrelated Docker resources.
No user secrets, live external accounts, deployments, real app launches or network services are
needed. Use current repo-supported configuration/OS paths, not machine-specific hardcoding.

All code comments, UI and maintained docs are English. Respect existing code style, package
versions and sibling ownership; inspect a reusable contract before inventing one. Do not edit
siblings except a genuinely missing reusable component contract, with explicit rationale,
correct owner tests and minimal scope. Do not reorganize shared standards in this task.

Local commits may be made using the owner's existing GPG configuration. Preserve signing and
use the same unlocked agent/session when available; never disable signing or replace keys.
No push, PR, merge, reset, rebase or deployment is requested by this handoff. Keep a coherent
signed history and explain any dirty files that were not yours. Preserve this sealed package
as historical input; maintained implementation/validation notes belong in the existing docs.

Finish with: actual entry/final SHAs and worktree/sibling state; WSC-R1 failing-first/final
result; completed API surfaces; dependency edges before/after; owner changes/compatibility;
exact test filters and counts; browser/publish/watch evidence; mandatory gate results; safe
cleanup. Report unresolved prerequisites honestly, never manufacture passing evidence.
Workspace is still partially complete: Data Sources and Storage remain deferred.
