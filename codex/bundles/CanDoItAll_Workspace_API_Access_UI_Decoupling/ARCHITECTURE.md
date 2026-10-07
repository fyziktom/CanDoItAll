# API Access: dependency and ownership design

## Primary invariant

A common Settings navigation surface does not imply a common backend or state owner.
API administration must not pull the existing Core sandbox into token persistence, password
hashing, identity validation or the broad Workspace implementation. Conversely, control-plane
stores must not reference a product UI/contracts project merely to provide it with DTOs.
The current Core graph and production slot provide the starting boundary. WS05, WS06,
WS11–WS14.

## Recommended composition

```text
Production SettingsPage (module host)
  -> generic WorkspaceSettingsSurface (unchanged API-independent shell)
  -> active API host / owner adapters
       -> API Access presentation (only if shared behavior warrants it)
       -> API Access UI -> API Access UI contracts + actual components
       -> existing authorization / issuer / user service / registry owners

API Access sandbox -> same API presentation + same API renderers
                   -> bounded scenario owners; no runtime implementation

Core sandbox -> existing Core presentation/renderers/contracts
             -> no API Access project, owner or runtime edge
```

Suggested locations (equivalent narrow names are acceptable with rationale):

```text
src/Modules/CanDoItAll.Modules.Workspace.ApiAccess.Contracts
src/UI/CanDoItAll.Workspace.ApiAccess.UI
src/Sandboxes/CanDoItAll.Workspace.ApiAccess.UiSandbox
```

An optional `Modules.Workspace.ApiAccess.Presentation` must share real state/effect policy,
not just forward methods. Do not create a separate project for every diagram box. The API
sandbox can host its actual feature surface in a BaseLib scaffold without importing Core
merely to duplicate navigation; production tests prove the real Settings slot.

## Allowed and forbidden responsibilities

| Boundary | Owns | Must not acquire |
| --- | --- | --- |
| API leaf contracts | Safe status, account/token metadata projections, captured UI requests, redacted action outcomes and real substitution ports | EF, Infrastructure, Workspace implementation, passwords hashes, deployment option trees, private token records |
| API UI | Actual form/table/dialog rendering, transient DOM/disclosure state, typed intents | Registry/file access, JWT/signing, access enforcement implementation, service locator |
| Presentation | Independent draft/query/overlay origins, admission, result reconciliation, retirement and bounded safe receipts | A universal Settings controller, token reconstruction, durable storage or permission inference |
| Module API host/adapters | Real service calls, canonical scope projection, current access checks, host lifetime and safe diagnostics | Reimplementation of stores, password hashing, configured administrator or HTTP auth |
| Existing owners | Registry/user durability, versions and authentication revisions, grant validation, session/issuer policy | Product UI dependencies or UI-supplied authority |
| Generic Core UI | Eight navigation entries and active content slot | API leaf/reference transitively, new general API service bag |

A UI-specific metadata projection is justified when it removes a real implementation edge.
Do not move `ApiTokenRecord` together with `ApiTokenSummary`, or `ApiAccessOptions` with
`ApiAccessStatus`, simply because they share a file. The private record contains credential
bindings; the options contain signing/configured-admin secrets. AP08, AP16.

## Contract strategy and compatibility

Prefer projecting safe data at production adapters rather than relocating the whole API
runtime. Existing service/wire models may remain where they are. Scope descriptions and
selection permissions come from the canonical `ApiScopeCatalog` through an adapter; no
second manually maintained authorization table. Text mentioning Projects or Storage does
not require a reference to their modules. AP07–AP09, AP16.

If extraction of a genuinely pure owner contract is unavoidable, justify the minimal scope,
choose its real owner and rebuild every consumer. Never make Infrastructure depend on a
new Workspace product contract, or fill broad Security.Abstractions with the entire API
product catalog. Namespace equality is not an assembly dependency; evaluate the real graph.
Preserve wire names, attributes, defaults, numeric kinds, expected versions, status mappings
and current serialization. Type forwarders are not automatic; add only for a demonstrated
binary consumer. A source rebuild is not a promise of arbitrary old-plugin binary compatibility.

The existing general `SettingsOperationLedger` is owned by Core UI. Do not make the API leaf
reference all Core UI merely to reuse its handful of receipt fields. A small API-specific
receipt is valid because tokens/accounts have different identities, authority and sensitive
lifetimes. Do not move a generalized mutation framework into SharedKernel for this task.

## Three separate origins

1. **Instance/control-plane origin:** account and credential records are persisted outside
   the selected business database. Pin the real host instance/control-plane context used by
   the current owner. A database switch is not a migration or recreation of these records.
2. **Validated caller/access epoch:** current trusted local operator or validated configured
   administrator. Use the actual access provider and recheck operations; UI availability is
   not permission. On caller/access retirement, stop new dispatch and discard sensitive UI.
3. **View/editor/operation origin:** the selected page, account ID/version, issuance draft,
   token ID, confirmation and request generation. Late completions may retain redacted
   original receipts, but cannot publish into another origin.

Do not serialize these into URLs or derive authority from subject text, a menu selection,
`canManage`, a database-profile GUID or a JWT decoded in the renderer. AP06, AP10–AP12.

## Production and runtime safeguards

Keep machine, ordinary-user and administrator-session categories distinct. Preserve configured
administrator ownership, no default ordinary-user business grants, expected-version conflicts,
authentication revision invalidation, exact GUID identity across username recreation and
existing login/stream/session enforcement. The broad machine compatibility scope and the
legacy issue label are not administrator credentials. AP09–AP12, AP15, AP16.

Do not edit global authorization, proxy/Blazor access, database profile runtime, memory provider
features or unrelated endpoint families. Small owner outcome fixes needed to keep a known
commit known are allowed, with real owner tests and affected HTTP compatibility proof.
Do not route UI through HTTP as a decoupling shortcut.

## Prove the graph, not the labels

Capture evaluated project/package/native/runtime closure for the original Web, final Web,
existing Core sandbox and new API sandbox in the same source-reference mode. Review source
replacement from Components/FileTools, public signature types, dynamic children, route discovery
and assets. Fail on unresolved/cyclic edges. Add negative fixtures for an indirect forbidden
edge and an unresolved dependency; do not treat missing assemblies as evidence of isolation.

The Core sandbox must not acquire an API leaf or API runtime dependency. Its old eight-project
count is historical evidence, not a timeless numeric assertion: compare semantic edges and
explain any unrelated checkout drift. An integrated production host legitimately contains both
features; do not confuse that with the standalone renderer/sandbox graph.
