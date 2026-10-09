# Native authoring authority: required bounded prerequisite

## Why this is separate

PC1's purpose was to separate rendering without redesigning persistence or runtime authority. Its maintained record correctly discloses the existing authoring lifetime gap. PC2 must not misclassify it as a new extraction regression, but cannot report durable Save/Publish parity while the actual native client loses the accepted data.

At the reviewed SHA the definition owner and role owner keep instance dictionaries, the native client creates fresh scopes, the catalog remains template-pack based, and the launch entry code consumes template selection. This is more than choosing a longer DI lifetime. [R06–R10, R25 in SOURCES.json]

**PC2 authorizes:** diagnosis through the actual client/composition, an implementation-ready causal/compatibility design, and a bounded adapter repair to an already-established durable owner if current source has one. It does **not** silently authorize new schema, a new canonical definition/publish model, mutation of distributed template files, or a changed runtime/launch contract. Such a redesign is a separate implementation slice, not a reason to stop P1/P2/P4/P5.

## Required executable diagnosis

Use a task-owned isolated PostgreSQL test endpoint and native DI. Do not reuse installed data, one manually constructed service instance, or a fake client that remembers expected values.

1. Through `IProcessWorkspaceProjectionClient`, load a known editable template-derived definition and record safe baseline identity/version.
2. Submit a legal unique draft change through the same client command path. Record the actual receipt and result version without treating it as proof of persistence.
3. Load through a **new service scope/client call**. Record whether identity, version, status and changed fields are retained. Repeat using a fresh root host/restart when testing durability; distinguish scope persistence from process persistence.
4. Cover the analogous role, step and template-import paths, and identify whether canvas edits are intentionally view/session state or durable definition data. A retained circuit-scoped canvas service is not automatically a durable definition store.
5. Check the catalog entry, draft/published counts, subsequent command concurrency and the origin's profile/project lifetime. Check whether a published authoring change affects a new launch's immutable prepared/instance plan; trace rather than assume.

Record actual outcomes and observed command/owner identities. A reproduction of a known gap is not a passing durability acceptance case. Do not add an always-green test asserting that Save loses data as the desired product contract. Keep a failing baseline reproducer in owned evidence or an explicitly separate diagnostic lane until the owner slice fixes it; never quietly skip required native parity or leave normal Stable broken.

## Owner and consumer map to produce

Create a maintained, sanitized `docs/architecture/processes-authoring-authority-prerequisite.md` (or a current canonical equivalent). Include exact current files/symbols for:

| Concern | Questions to resolve |
| --- | --- |
| Canonical identity | Template key, definition ID, draft ID, published version; stable versus generated identity; global/project scope |
| Definition authoring | Which owner commits identity/governance/contracts/simulation state, and how other editor families share that aggregate/version |
| Roles and steps | Stable row keys, workflow preferences, decision-role bindings, contracts/branches/artifacts/subprocess mappings; referential integrity on Add/Delete/import |
| Templates and catalog | Immutable distributed defaults versus editable overlays/copies, precedence, metadata/count/search updates, safe reset/delete behavior |
| Native persistence | Existing authoritative store candidates and actual usages; transaction, concurrency and schema conventions; profile isolation and migrations |
| Project authority | Captured database/profile/project lifetime, admission enforcement before actual writes, retirement/recreation and transfer/delete participants |
| API | Existing definition reads/mutations and response compatibility; do not change transport just to repair UI |
| Publish and launch | Exact template resolution/compilation/prepared plan path; which committed version a new launch executes; old accepted runs must keep immutable plans |
| Lifecycle | New scope, application restart, migration from template-only installations, import/export, duplicate IDs and cleanup |
| Tests/operations | Targeted owner/API/component/browser consumers, isolated PostgreSQL requirements, migration rehearsal and rollback/backup obligations |

Map the real symbols with CodeAnalytics plus callsites and source. The review only inspected the launch service entry file/range; the executor must inspect relevant partial files and actual resolvers before making any launch claim.

## Decision path

**Existing authoritative owner found on current checkout:** demonstrate it is already the canonical write/read authority with correct profile and project admission. A narrow native adapter/lifetime repair is in scope when it introduces no new schema or changed publication/launch semantics. Keep the UI contracts light and test cross-scope/restart, conflicts and affected API/catalog consumers. Record why this is an adapter correction rather than a new authority design.

**Only dictionary/template authority remains:** deliver a separate implementation-ready slice, not a vague TODO. Name the smallest affected aggregate(s), target placement, exact files/consumers, lifecycle and transaction/version choices, whether a migration is necessary, compatibility of existing defaults and accepted run plans, phased implementation and test strategy. Explicitly compare a native persistent overlay/store with other current owner options. Explain rejected shortcuts and potential performance/data-isolation effects. Preserve the current engine behavior while leaving authoring durability visibly unresolved.

A proposed store interface or table name in that plan is a **design proposal**, not a claim that it exists. The plan must not require all historical bundles to be replayed. Prefer one authoritative native aggregate/commit boundary over disconnected per-panel stores with incompatible versions. Reuse existing IDs and serializer/version conventions when appropriate, without making renderer types the database authority.

## Prohibited shortcuts

- Replacing Scoped dictionaries with a process-wide singleton as the durability fix.
- Persisting native authoring in the light UI library, browser state, or mutable template-pack source files.
- Claiming one service instance, a sandbox echo or a success label proves restart durability.
- Changing Publish into a label-only operation while launching unrelated defaults, or silently changing the template that existing accepted runs reference.
- Clearing expected versions, bypassing project admission, re-binding a retired public project ID, removing save/publish controls, or inventing broad retry-on-exception writes.
- Creating a new HTTP API, generic event framework or a full runtime rewrite merely to repair one native ownership boundary.

## Required delivery and readiness

The PC2 final report includes the real-client diagnostic command and outcome, source checkpoint, exact owner/callsite map, completed bounded adapter repair **or** the separate implementation-ready native scope, and the remaining native parity status. If the dictionary path remains, `authoring_durability` cannot be PASS and the overall feature/release status cannot be unqualified green. Structural decoupling and implemented editor corrections can be reported separately.
