# P07 Agent policy access

The native planning checkpoint is signed as `bd89b5254`. This slice exposes the existing
Agent policy owner through the catalog and HTTP; it does not activate arbitrary tools,
Process or Workflow policies. Their independent material/claim/child fences remain required.

## Ownership and concurrency

AgentDecisionDataPolicyOwner and the file catalog remain the only policy writer.
The original workspace/profile generation, Agent ID and whole-catalog revision are
required. This is intentionally coarse and can conflict on another Agent's edit;
splitting catalog storage is not justified by this editor. No lock spans editing,
network or model work. Ordinary Agent saves preserve reserved policy metadata, and a
policy save advances the ordinary Agent version as well as catalog revision.

Use a separate catalog policy dialog, not a separate save inside the ordinary Agent
editor. The policy dialog owns its read, mutable draft, frozen submission, pending
comparison and outcome. It must never advance another editor's original version.
An already open ordinary editor continues to fail its existing CAS after policy changes.
The catalog may reload after known acceptance without rewriting another editor's draft.

Renderer: AgentFramework.UI/Catalog; typed nullable override fields and rendering only.
Host: Modules.AgentFramework; workspace read/write, original opening/profile lifetime,
busy gate and result routing. A small independently tested session owns draft/result
transitions. No new service interface or production partial is needed. Catalog dispatch
adds one typed intent and one dialog composition path. Workspace.UI is not a dependency.

Blank override fields inherit; zero replans is an explicit value. Durations use invariant
constant format and must be positive when present. Validation uses the existing strict
override codec. A setting does not promise a host capability: actual admission still
resolves workspace and owner limits and rejects unsupported combinations. Existing
admitted decisions retain their captured policy.

## Outcomes and reconciliation

Freeze the complete override and original expectation before dispatch. Later typing
survives an acknowledged save. Acceptance uses only that command's exact next snapshot;
it is not inferred by a later read. Rejection retains the draft and original expectation.
Refresh reads current policy for comparison; adopting it or explicitly rebasing the
retained draft is a separate user action. Failed refresh cannot enable a save with an
invented current expectation. Late responses from a retired opening/profile cannot alter
the next dialog. A failed postcommit catalog refresh must not turn acceptance into failure.

The existing owner does not persist an operation replay receipt. Unknown outcomes must
remain marked unknown, retaining the original operation and payload; matching current
policy does not prove which command committed. No automatic retry, automatic rebase or
claim of durable replay is permitted. This limitation remains an explicit E2E follow-up.

## HTTP boundary

GET/PUT `/api/agents/{agentId}/decision-data-policy` use existing read/write Agent
permissions. Transfer the two Int64 revisions as canonical decimal strings in a typed
wire expectation; clients must not convert them to JavaScript numbers. Domain contracts
remain unchanged. The override uses the existing bounded version-one codec through a
property converter, avoiding a global serialization change to saved planning provenance.
Require exact route identity, nonempty operation ID, all required expectation fields,
strict known/unique JSON members and an 8-KiB body bound. Reject malformed requests
before dispatch. Return distinct accepted, rejected and unconfirmed outcomes; omit
private exception messages and configuration. Current-read failure is not an empty policy.

## Architecture and proof gates

CodeAnalytics baseline `snap-20261019124718-70e88475` covers Editor.UI, AgentFramework.UI,
the module and Web: 477 documents, 1,310 types, 10,907 members, 75 registrations.
Two pre-existing module/type cycles remain. The 189 WS0002 advisory diagnostics and
32 dependency/DI interpretation diagnostics limit static analysis; direct builds and
real composition proof are mandatory. No project reference or DI registration is planned.
AgentDetailsDialog already has 129 members and is not extended for this independent owner.
The existing AgentFramework sandbox already references AgentFramework.UI. Keep this
catalog-launched independent editor there, avoiding an additional sandbox project edge.
One Web OpenAPI schema-transformer registration documents the strict property converter
and lossless revisions. It reuses the existing bounded Int64 revision pattern. The shared
Agent JSON unique-member check also serves planning actions, whose HTTP lane must rerun.

Test pure session transitions, nullable inheritance, invalid input, competing saves,
later typing, stale reads and unknown acknowledgement without constructing the runtime.
HTTP proof must cover real file-owner CAS and ordinary-save preservation, malformed and
oversized requests, lossless Int64 transfer, permissions, changed scope and sanitized
failures. Native bUnit/large-desktop browser proof must open the actual catalog dialog,
change policy, race another actor, preserve the draft and require explicit reconciliation.
The backend-free scenario must exercise inherited, invalid, conflict and unknown states.
Build affected standard Release projects, confirm focused discovery, run final full-source
portability enforcement and update the rendering register. Compare dependency direction
and responsibility growth at closure. Keep SPA on its existing branch without merging.
