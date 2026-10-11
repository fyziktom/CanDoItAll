# Publication and exact executable resolution

## Selection policy

Implement one native resolved-definition envelope for a specific admitted scope and chosen
source. It carries stable definition identity, immutable executable content identity, schema,
provenance, resolved resources/dependencies and the source of launchable status. The UI sees
light display/value projections, not the native service or authority object.

For a project launch, the selected eligible project publication takes precedence over an
eligible global publication, then an unchanged distributed template. Drafts are editable
but do not silently become executable publications. A draft-only override may coexist with
an inherited runnable source, but the UI must name that source/version explicitly. A selected
archived override blocks the affected new launch; do not silently fall through to a template
with the same key. Missing/denied/unreadable data is an error, not fallback permission.

Catalog actions and backend selection must agree. An explicitly requested definition cannot
be replaced by the first available template because lookup failed. Preserve current supported
implicit-default selection separately from an explicit choice. Use database-profile and
project-incarnation identity in lookups; stable definition ID alone does not establish scope.

## Save, Publish, Archive and Delete

SaveDraft stores only the authored draft; an earlier immutable publication remains runnable
until explicitly superseded/archived under the lifecycle policy. Publish validates structural
references, schema and supported executable contracts using existing validators/compiler
boundaries, then atomically installs a publication pointer and receipt. Do not run inference
or actual workflow execution during publication. Dynamic runtime capability/readiness and
approval checks still happen at launch; publishing does not grant them.

The publication freezes supported execution guidance/resources and resolved semantic data.
Hashes are integrity/version identities, not caller authorization. Repeated recovery of one
Publish operation returns the same publication; timestamps or newly generated projection IDs
must not change semantic version identity by themselves.

Archive blocks fresh preparation/new launch acceptance for that source while retaining
historical publications and accepted runs. A previously unaccepted prepared preview must
retain its reviewed content identity but be rechecked for current launchability before
acceptance; if archived, refuse rather than silently recompile another revision. An already
accepted run continues from its immutable inputs subject to existing current authority rules.

Delete preserves the reviewed local-entry/reset-to-inherited meaning where allowed, with
explicit labeling and native policy. It cannot erase a distributed template or immutable
history still referenced by preparations/runs. Restoring inherited behavior is the consequence
of an explicit allowed Delete/reset, not an automatic side effect of Archive or a failed read.
For a newer current API with distinct draft-delete/archive/restore actions, retain that more
precise existing contract rather than collapsing it.

## Consumer map that must be completed from current source

| Consumer | Required integration |
| --- | --- |
| ProcessDefinitionCatalogProjectionService and shell projections | Show actual draft/published/archived state and source; resolve selected revision consistently |
| ProcessLaunchApplicationService.ResolveDefinition / PrepareLaunchAsync | Resolve the selected canonical publication once; feed exact content to kernel, compiler, executors and assignments |
| ProcessLaunchVariablePreparationService and its callers | Use the same resolved source/activations, not a second current template lookup |
| ProcessTemplateKernelBuilder | Preserve stable default IDs/content/guidance hashing; compile authored content without lossy reconstruction |
| ProcessLaunchApplicationService.Prepared.cs | Reuse saved caller-intent/preparation before new resolution; no mutation of reviewed/accepted plan on republish |
| Runtime assignments, step brief and recovery consumers | Use captured immutable contracts/variables/resource references; never consult today's mutable draft |
| Subprocess launch coordinator producers and parent/child artifact bridge | Resolve/pin child executable version and typed contract consistently; new template/overlay changes cannot silently retarget an existing parent |
| Workbench / standalone / API / agent-tool launch producers | Preserve request/authority/link/idempotency semantics and exact selected source across all existing entry points |
| Existing-launch lookup | Scope-safe identity and matching variables, no accidental reuse from another profile/project incarnation |

The inspected ProcessSubprocessContractResolver is already a pure decoder of captured launch
variables. Keep it pure. Audit actual subprocess launch producer implementations with fresh
CodeAnalytics/source references; do not assume changing this decoder fixes child selection.

## Empty is not unresolved

The current launch-variable helper reloads template activations when its supplied list is
empty. A publication can intentionally have no driver activations. Distinguish resolved-empty
from unresolved using the envelope/state contract; do not silently enable default template
drivers. Reuse existing synchronous enrichment only when it receives an already resolved
snapshot. Do not block on new asynchronous database resolution using .Result/GetAwaiter in
a sync callback; move resolution to the native async boundary and adapt actual callers.

## Immutable dependency/recovery contract

Pin the exact child/content/resource identities necessary for the reviewed parent to execute
as planned. A finite resolution closure can share immutable content by hash; no full object
copy per runtime tick. Preserve supported recursion/loop budgets and existing default semantics;
detect unresolved/conflicting dependencies deterministically rather than following unbounded
recursive loading. Do not add a blanket legacy-template ban as a shortcut.

A delayed child launch must not select the newest publication by key if the accepted parent
review pinned an earlier child. Persist required dependency references in the existing prepared/
plan/assignment contracts with compatible serialization. The exact small extension is a design
choice, but the old prepared launch and parent/child outcome semantics remain non-negotiable.

If stored content is unavailable/corrupt or a referenced schema is unsupported, return a safe
reconciliation/blocked result. Never replace it with a same-key current template. Repair is
explicit and does not rewrite historical receipts. Existing project/actor authority is still
rechecked at the native boundaries; immutable content does not make permission immutable.

## Required executable proof

Create an edited definition whose semantic change is observable in a typed deterministic
workflow outcome or plan/assignment contract, not just its title. Publish v1, preview/persist
its preparation, change draft and publish v2, then verify that the original preparation uses
v1 and a new explicit preview uses v2. Restart an owned application process against the same
isolated DB and repeat native read/accept/recovery. Include a parent whose child publication
changes before dispatch; validate the pinned child and output mapping. Preserve an untouched
distributed-template launch with the same stable IDs/contract semantics as before PC3.

Maintain current accepted-run, postcommit warning, continuation lease, current-authority,
workflow artifact/result and Workbench-link tests. Do not downgrade them to mock Accepted
responses or describe a scripted inference fixture as a live-model execution.
