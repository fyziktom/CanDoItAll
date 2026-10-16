# Projects definition contracts and cutover

Status: design decision, reviewed against architecture commit `ceb10731e` and the
current source on 2026-10-16. No contract, route, migration, or test described as
proposed below is evidence of implemented behavior. This refines
[the owner protocol](01-owner-transaction-design.md) and
[the editor design](03-editor-and-validation-design.md), with the caller, dataset
and finite epoch lifecycle specified in [06](06-operation-lifecycle-contract.md);
it does not reduce their
final receipt, authority, projection, or restore requirements.

## Delivery boundary: original state first, durable retry next

Use two implementation checkpoints with different, explicit guarantees:

| Checkpoint | Required behavior | Excluded claim |
| --- | --- | --- |
| P02a: conditional definition writes | Coherent definition reads; mandatory original definition expectation on every existing-project update; root CAS for full and scalar writes; correct child intent; exact accepted definition acknowledgement; all current update adapters converted or explicitly refused | Durable retry, response-loss recovery, restart-safe operation lookup, complete Projects pilot closure |
| P02b: protected operation contract | P02a plus required caller-supplied operation identity, trusted caller binding, atomic exact result receipt, lookup/replay, finite owner epoch retention, namespace admission and atomic mandatory projection | Generic engine-wide safety or external effect deduplication |

P02a is a useful development commit and can reject lost updates immediately. It
is not the final externally advertised concurrent-mutation feature. Do not add
an accepted `operationId` to P02a and merely log it: that would imply a guarantee
the transaction does not provide. No automatic mutation retry is enabled there.
An unconfirmed one-shot save remains unconfirmed. A later current read cannot
prove which operation committed.

Implement the exact command/acknowledgement vocabulary in P02a so P02b can wrap
it without a second meaning for replacement versus scalar intent. Introduce
the new protected HTTP operations only when their receipt protocol works. The
existing POST can enforce original-state safety during P02a; its GUID response
is not an accepted-state or recovery contract.

## P02a public contract

Place data-only types in `CanDoItAll.Modules.Projects.Contracts`, using its
existing namespace `CanDoItAll.Modules.Projects`. Do not move
`ProjectWriteAdmission` to SharedKernel or change its lifecycle meaning.

The following signatures specify the contract. Constructor validation and JSON
required-member handling are implementation work; nullable/default binding must
not turn an invalid request into an unconditional write.

```csharp
public sealed record ProjectDefinitionExpectation {
    public ProjectDefinitionExpectation(ProjectWriteAdmission project, Guid revision) {
        ArgumentNullException.ThrowIfNull(project);
        if (revision == Guid.Empty) {
            throw new ArgumentException("A definition expectation requires a nonempty revision.", nameof(revision));
        }
        Project = project;
        Revision = revision;
    }

    public ProjectWriteAdmission Project { get; }
    public Guid Revision { get; }
}

public sealed record ProjectDefinitionScalars(
    string Name,
    string Description,
    string Objective,
    ProjectStatus Status,
    string CurrentPhase,
    DateTime? TargetDateUtc);

public sealed record ProjectDefinitionExternalIdentity(string Namespace, string Key);

public sealed record ProjectDefinitionPhaseInput(
    Guid? Id,
    string Name,
    string Goal,
    ProjectPhaseStatus Status,
    DateTime? StartDateUtc,
    DateTime? EndDateUtc);

public sealed record ProjectDefinitionOptionInput(
    Guid? Id,
    ProjectOptionCategory Category,
    string OptionName,
    string Notes);

public sealed record ReplaceProjectDefinitionCommand(
    ProjectDefinitionExpectation Expected,
    ProjectDefinitionScalars Scalars,
    IReadOnlyList<ProjectDefinitionPhaseInput> Phases,
    IReadOnlyList<ProjectDefinitionOptionInput> Options,
    ProjectDefinitionExternalIdentity? AssignExternalIdentity);

public sealed record UpdateProjectDefinitionScalarsCommand(
    ProjectDefinitionExpectation Expected,
    ProjectDefinitionScalars Scalars);

public sealed record CreateProjectDefinitionCommand(
    ProjectDefinitionScalars Scalars,
    IReadOnlyList<ProjectDefinitionPhaseInput> Phases,
    IReadOnlyList<ProjectDefinitionOptionInput> Options,
    ProjectDefinitionExternalIdentity? ExternalIdentity);
```

`ProjectDefinitionExpectation.Project` is the original profile/project/lifetime
tuple; `Revision` is a nonempty opaque GUID equality stamp. There is no accepted
empty admission, empty revision, wildcard, `Force`, or `IgnoreVersion` option.
The revision is not ordered and does not authorize access. A route project ID
must equal the expectation's project ID.

These are concrete intent shapes, not interchangeable generic patches:

- Replacement requires both collections, even when empty, and replaces their
  complete persisted contents. Missing/null collections reject; they do not
  default to empty. Each nonempty child ID must belong to this project and occur
  once. Null child IDs create rows; empty GUIDs reject. Create rejects all
  persisted child IDs. The owner snapshots the lists and nested values before
  its first asynchronous boundary; `IReadOnlyList` alone is not immutable.
- Scalar update never loads replacement lists from the request and never
  synthesizes empty lists. It preserves phase/option rows and external identity.
  It still advances the same definition revision, since the UI's full proposal
  may depend on those scalars. There is no field-level merge promise.
- `AssignExternalIdentity = null` means preserve the existing identity, not
  clear it. A pair binds an unassigned identity or repeats the same normalized
  identity; changing an assigned pair rejects. The existing
  `ProjectExternalIdentity` normalizer stays in the owner. A create's null pair
  means no assignment. The scalar command deliberately cannot assign identity.
- Preserve existing scalar/date normalization and enum meanings. Reject undefined
  enum values. Name is required. Phase order follows submitted array order.
  Preserve submitted option order in acknowledgement mapping; do not rely on
  database iteration order. A blank optional option is absent from the accepted
  set, including when it had a persisted ID. Synthetic empty categories are
  editor conveniences, not persisted rows or accepted rows.
- Accepted normalized no-change saves still advance the definition revision and
  UpdatedAt. Stale requests never bypass the predicate by comparing field values.

### Existing editor DTO and method compatibility

Add `ProjectDefinitionExpectation? ExpectedDefinition { get; set; }` to
`ProjectEditorModel`. It is serialized and returned by coherent existing-project
reads. It is nullable because the same legacy class describes a new draft; owner
validation requires it whenever `Id` is present. `ExpectedLifetimeId` and
`ExpectedProjectAdmission` remain compatibility mirrors, not alternate
preconditions. If either is supplied it must agree with `ExpectedDefinition`;
neither alone authorizes an update. A create rejects an unexpected definition
expectation rather than quietly ignoring it.

Preserve the current scalar request's positional call sites while adding the
serialized property explicitly:

```csharp
public sealed record ProjectStructureProjectSaveRequest(
    string Name,
    string Description,
    string Objective,
    string CurrentPhase,
    ProjectStatus Status = ProjectStatus.Draft,
    DateTime? TargetDateUtc = null,
    string? LeaseToken = null) {
    public ProjectDefinitionExpectation? ExpectedDefinition { get; init; }
}
```

It is nullable only because the same request currently also creates projects.
The existing-project adapter requires it before any effect and maps every desired
scalar to `UpdateProjectDefinitionScalarsCommand`; it cannot use omitted/default
children as a replacement. A lease token and a current lifecycle admission do not
replace `ExpectedDefinition`. P02b adds its operation metadata in the protected
request contract, not a cosmetic optional ID in this checkpoint.

Preserve these existing public signatures as P02a forwarding methods:

```csharp
Task<Result<Guid>> SaveAsync(
    ProjectEditorModel model,
    CancellationToken cancellationToken = default,
    ProjectMutationAuthorization? authorization = null);

Task<Result<ProjectEditorAcknowledgement>> SaveEditorAsync(
    ProjectEditorModel model,
    CancellationToken cancellationToken = default);
```

The facade snapshots/maps the legacy DTO once, selects create or replacement,
and delegates to the new top-level definition owner. It never reads a new token
to complete the request. `SaveAsync` maps accepted target ID from the exact
acknowledgement; `SaveEditorAsync` returns the acknowledgement. Keep reservation
and compensation create overloads forwarding through the same create owner;
preserve their stronger source/reservation checks. A scalar adapter must call
the scalar owner method, not either full-replacement facade.

Use a cohesive top-level `ProjectDefinitionWriter` and a bounded coherent read
method/service. Do not append another ProjectsService partial. Public contracts
need no EF, Web, Workbench, Process, MAF, or UI references. The writer retains the
existing Projects-owned source authority port and transaction mechanics.

### Exact accepted result

Change the first positional member of `ProjectEditorAcknowledgement` from
`ProjectWriteAdmission Project` to `ProjectDefinitionExpectation Definition`.
Keep a derived `Project => Definition.Project` property so existing consumers
of the lifecycle tuple do not change semantics. Add the accepted normalized
`ProjectDefinitionExternalIdentity? ExternalIdentity`. Retain the existing
scalar fields and both child acknowledgement lists, including `SubmittedIndex`.
Update all constructor call sites; do not make the new definition nullable just
to preserve construction of incomplete acknowledgements in tests.

The full constructor contract is therefore:

```csharp
public sealed record ProjectEditorAcknowledgement(
    ProjectDefinitionExpectation Definition,
    string Name,
    string Description,
    string Objective,
    ProjectStatus Status,
    string CurrentPhase,
    DateTime? TargetDateUtc,
    IReadOnlyList<ProjectPhaseAcknowledgement> Phases,
    IReadOnlyList<ProjectOptionAcknowledgement> Options,
    ProjectDefinitionExternalIdentity? ExternalIdentity) {
    public ProjectWriteAdmission Project => Definition.Project;
}
```

There is no trailing optional/default accepted definition. A real accepted result
must carry it, and test fixtures must obtain/build a valid explicit expectation.

Add a distinct `ProjectDefinitionScalarsAcknowledgement` containing
`ProjectDefinitionExpectation Definition`, `ProjectDefinitionScalars Scalars`,
and `ProjectDefinitionExternalIdentity? ExternalIdentity`. It has no child lists.
An empty list must never be used to mean "not part of this scalar result".

The acknowledged revision is allocated for this accepted write and the accepted
values/child IDs are captured inside its transaction. It is not the result of
`GetAsync` after release. Full acknowledgements contain every accepted persisted
phase/option in accepted order; their submitted indices refer to the frozen
submission, not the current editor's list. Blank omitted option indices have no
accepted row. Existing UI reconciliation must additionally handle an omitted
persisted option, so a cleared row cannot retain an ID that no longer exists.

Protected definition reads return a separate `ProjectDefinitionSnapshot`:
`Expectation`, `Scalars`, `ExternalIdentity`, and ordered persisted phase/option
values with nonnullable IDs. Read rows have no `SubmittedIndex`. This avoids
pretending a read is an acknowledgement. The existing `GetAsync` can map this
snapshot into `ProjectEditorModel` and then add empty category placeholders.
An explicit protected read of a missing ID returns not-found; it must not return
a new project template, as current `GetAsync` does. Creation templates remain a
separate local/read action.

### Error mapping

Add named constants in the existing `ProjectErrorCodes`; keep `Error` as the
ordinary rejected result in P02a. Use these external code values consistently:

| Constant / proposed value | Meaning | HTTP mapping |
| --- | --- | --- |
| `DefinitionExpectationRequired` / `projects.definition-expectation-required` | Existing target without a complete original expectation | 400 |
| `DefinitionExpectationInvalid` / `projects.definition-expectation-invalid` | Empty/contradictory identities, revision, route target, or compatibility mirrors | 400 |
| `DefinitionConflict` / `projects.definition-conflict` | Same admitted lifetime with an intervening accepted definition write | 409 |
| Existing `LifetimeChanged` | Original lifetime no longer admitted | 409 on definition operations |
| Existing `NotFound` | No admitted existing target available | 404 |
| `DefinitionChildInvalid` / `projects.definition-child-invalid` | Duplicate, foreign, or unknown persisted child identity | 400; do not disclose the foreign owner |
| `DefinitionLimitExceeded` / `projects.definition-limit-exceeded` | A declared aggregate/byte limit would be exceeded | 413 for HTTP size limits; owner error code retained |
| Existing external identity conflict/immutable codes | Existing identity invariant failed | 409 |

The endpoint does not emit the latest token as a replacement precondition in an
error. A separate authorized coherent read retrieves current state for explicit
reconciliation. Transaction serialization errors require a fresh context before
classification; never expose a misleading current state from the losing snapshot.
Cancellation/connection loss after dispatch must not be mapped to a known
rejected result solely because the request task failed.

## P02b operation contract and exact result bytes

The implementation card is [P01b/P02b durable results](../../../codex/bundles/concurrent-mutations/plan/P02b-durable-results.md).
It identifies exact reuse of the current writer, coordinated Search participant,
control-plane file mechanics and source-authority ordering. It is not execution proof.

### Required identity and trusted context

The new protected write envelope is
`ProjectDefinitionRequest<TCommand>(ProjectDefinitionOperation Operation, ProjectDefinitionRequestVersion Version, TCommand Command)`.
`ProjectDefinitionOperation` contains a typed nonempty `DatasetNamespaceId`, a
typed positive 64-bit `ProjectOperationEpoch`, nonempty `Guid Id`, and required
`ProjectCallerBinding CallerBinding`. Request version contains positive schema and
normalization/digest version numbers; the first implementation supports `(1, 1)`.
The neutral
dataset value is defined in SharedKernel; the epoch and operation contracts belong
to Projects.Contracts. The namespace comes from control-plane activation and the
epoch/deadlines from Projects owner admission. The caller allocates the operation
ID once before the first dispatch and retains the entire address with the frozen
command across timeouts/retries. New or manually reconciled intent receives a new
ID and an admitted current epoch. An undispatched draft may obtain current operation
context without changing its original target expectation; an unconfirmed submission
must never refresh only its epoch, namespace, caller or ID to retry.

The owner metadata supplies the current caller binding from independently verified
server identity. Compare the retained binding before lookup or effect; it never grants
authority or chooses a caller. Wrong/missing/malformed binding is refused, and a valid
different binding returns `projects.caller-context-changed`. Thus a pending OpenApi
operation cannot become a new AuthenticatedSubject operation after restart/account
change. Lookup takes the required binding as a bounded `callerBinding` query parameter;
no caller ID is accepted. The opaque value is correlation, not a credential.

Retain schema and normalization/digest version in each pending request and receipt;
dispatch only through that supported version's normalizer/codec. Result codec version
remains separate. Keep comparison and decoding support through advertised horizons.
Unsupported versions return `projects.operation-version-unsupported`, without execution
or reinterpretation. Equivalent normalized inputs can share a digest, but preserve the
original frozen values separately for reconciliation. New desired intent uses a new ID.

The dataset namespace is an activation fence, not a process counter. It must
survive normal restart and change during destructive replacement under the
maintenance protocol. The current constant/process-local runtime generation
cannot satisfy it. Updates retain the definition expectation in addition to
this operation namespace; creates have the namespace but no nonexistent target
expectation. Reject an old namespace before admitting a new effect. Do not
advertise restore-safe replay until the activation owner implements this fence.

An application-only `ProjectDefinitionInvocation` carries the trusted durable
caller identity and existing source authority/basis bindings. It is not JSON
request data. The protected writer receives it explicitly; it must not infer
the caller from `ProjectMutationAuthorization` being null or from a body actor.
Enabled HTTP composition adapts the existing verified principal/current scope
checks, including `WebCurrentPrincipalResolver` where applicable. Durable
authenticated caller equality uses trusted issuer, subject and caller kind; it
excludes JWT `jti`, circuit ID, current token expiry and the resolver's changing
authorization `Stamp`. Those values remain relevant to current authorization,
not deduplication identity.

Disabled-authorization HTTP composition explicitly supplies `OpenApi`; native
Projects composition supplies `LocalWorkspace` under the feature's existing access
model. These are fixed, distinct server-selected workspace trust scopes, not
authenticated people. Preserve these existing modes without adding anonymous
correlation credentials or forcing native authentication. Never select either
scope after authentication failure, from a null authorization argument, or from
request actor/header/IP text. Enabled mode failures remain denials. Auth mode or
surface changes do not automatically remap an old operation's caller scope.
Agent/process adapters use the dedicated `GovernedExecution` mapping of real
admitted execution/source identity when their integration slice ships, never model
text. See 06 for the exact identity and current disclosure rules.

Current write/source authorization remains checked at effect time through the
existing authority protocol. Replay/lookup needs current read permission for the
historical accepted payload even when the original effect authority has expired.
An HTTP lookup requires current `ReadProjects`; a retrying POST also obeys its
route's `WriteProjects` policy. Returning an authorized
prior result is different from executing the command again. The Projects module
must not reference `WebCurrentPrincipalResolver`; the composition adapter owns
that dependency. A caller value by itself is not an authorization proof.

For P02b add typed overloads/protected methods taking the operation envelope and
the explicit invocation. Convert native UI, HTTP and Workbench adapters to them.
Keep contextless public signatures for source compatibility if needed, but after
cutover they must return a documented `projects.protected-write-context-required`
failure for ordinary writes. Leaving a successful contextless path executable
would preserve an unjournaled bypass. Do not manufacture a random operation ID
or infer a shared workspace caller inside the compatibility facade; only the
explicitly composed adapter may select `OpenApi` or `LocalWorkspace`. Controlled
maintenance writes remain a distinct locked protocol, not a nullable bypass.

### Owner outcome and HTTP representation

Use an owner-specific discriminated result, with these mutually exclusive cases
and concrete acknowledgement types for full versus scalar operations:

| Case | Required data | Meaning |
| --- | --- | --- |
| Accepted | Original `ProjectDefinitionOperation`; exact accepted acknowledgement; accepted timestamp; codec version; `Replayed` flag | Effect is confirmed; replay returns the same accepted content |
| Rejected | Typed/named errors and operation reference where available | This attempt is known to have made no effect; never used for a lost commit acknowledgement |
| Unconfirmed | Original operation reference and stable recovery code | Commit status could not be established; lookup or exact replay is required |

Rejected describes this attempt. If an earlier dispatch is unconfirmed, a later
rejection and fresh NotObserved do not establish that no effect exists. Lookup uses
distinct Found, NotObserved(Open/Draining), ClosedAbsent, OperationExpired,
DatasetChanged, CallerContextChanged and DetailUnavailable cases. ClosedAbsent requires
an authoritatively closed, fully retained epoch and includes closure/observation times;
only it or exact Found resolves prior uncertainty. Retirement/denial/expiry never does.

`Result<Guid>` cannot express all three cases and is not the protected owner
contract. Its legacy response adapter can still return a GUID for a confirmed
accepted operation, but must preserve unconfirmed status and its recovery
reference rather than return an ordinary validation error. Do not add generic
mutation outcomes to SharedKernel solely for this pilot.

Persist canonical UTF-8 bytes for the versioned accepted payload, alongside its
typed metadata. The accepted payload includes the operation reference, accepted
timestamp, exact definition expectation, normalized accepted values, external
identity and full child mappings (or scalar-only acknowledgement). Assign the
timestamp once before constructing the payload in the successful transaction.
Replaying must not reserialize a later read or regenerate timestamps/IDs.
If current field/target permission no longer permits the exact accepted payload,
return a typed detail-unavailable/access refusal and preserve the draft. A redacted
payload or newer owner read must not be presented as the exact acknowledgement.
Exclude unrelated LLM context, provider credentials and infrastructure secrets.

The accepted payload bytes are immutable for the receipt codec version. Normal
HTTP headers, response correlation IDs and the outer `replayed` flag need not be
byte-identical. Use a versioned codec with checked-in golden semantic/byte cases
so a serializer upgrade cannot silently reinterpret a retained payload. Store
the payload directly for this pilot rather than introducing an immutable full
definition version store merely to reduce duplication.

The semantic digest covers command kind/version, ordered normalized fields and
arrays, original expectation, the full original operation address (namespace,
epoch, trusted caller scope and operation ID), external identity intent,
reservation/target identity when provided, and effect-relevant admitted
basis/approval bindings. Null new-row IDs stay null in the digest; newly allocated
accepted IDs do not change request identity. Include option/phase order and blank
submitted rows' index positions because accepted indices depend on them. Do not
hash mutable HTTP correlation, credential tokens or retry timestamps. Same ID
with a different digest rejects as `projects.operation-payload-mismatch`.

Receipt uniqueness is the tuple of dataset namespace, Projects epoch, trusted caller
scope and operation ID. Validate the request's original namespace and epoch through the
current dataset/owner admission protocol, not an unvalidated cache.
Complete duplicates return the old result before rejecting its now-stale content
expectation. Competing insert/serialization failures are reconciled in a fresh
canonical context after rollback, not in the losing transaction snapshot. No
precommitted generic InProgress row or background executor is introduced.

An open epoch admits missing operations only before its deadline. Closing it drains
already admitted transactions under the shared/exclusive owner epoch guard; a closed
epoch cannot execute a missing operation. Existing authorized receipts remain replayable
within their declared horizon. A durable retired-epoch watermark rejects old addresses
after bounded receipt deletion. In-flight work during closure can remain unconfirmed;
expiry never proves the historical operation rolled back. Routine epoch rotation does
not rotate the dataset namespace, and neither lifecycle changes a target edit revision.

### Boundaries and candidate capacity settings

Use named, validated owner options. These are proposed initial values for retained
data preflight and load measurement, not claims that today's data already fits:

| Option | Candidate value | Enforcement |
| --- | --- | --- |
| `MaxPhaseCount` | 1,024 | Submitted count before copying/transaction |
| `MaxOptionCount` | 256 | Submitted count, including blank placeholders |
| `MaxLongTextUtf8Bytes` | 65,536 per Description, Objective, Goal or Notes | Before normalization/copy growth; retain existing shorter field schema limits |
| `MaxRequestUtf8Bytes` | 2,097,152 | Streaming HTTP body guard before deserialization; in-process commands also have aggregate bounds |
| `MaxCanonicalCommandUtf8Bytes` | 1,048,576 | Exact canonical serialized command, before owner mutation |
| `MaxAcceptedPayloadUtf8Bytes` | 2,097,152 | Bounded provisional result before DML; exact final bytes after token stamping and before commit; reject and roll back the whole command if exceeded |

Count UTF-8 and serialized bytes, not C# character counts or an assumed compression
ratio. JSON escaping and server-assigned IDs can expand the acknowledgement.
Check submitted counts and field/aggregate bounds before collection copying and
normalization allocations. The Web adapter bounds the body before model binding and
scans bounded JSON tokens/counts before constructing immutable commands; native/direct
adapters use the same bounded command construction entrypoint before copying lists.
Verify the accepted byte bound after constructing normalized values/mappings;
there must be no "commit first, discover receipt too large later" path. Test the
actual default, maximum and one-over boundary for every independent limit.
Always reject rather than truncate. A configured larger limit requires a measured
capacity decision and aligned transport/storage limits.

The current ApplicationManagedConcurrencyTokens.Stamp changes the modified root's
revision during SaveChanges. Serialize the final accepted revision after that flush
inside the transaction, enforce the exact byte bound, and commit only after receipt
and enlisted search succeed. A provisional serialization is an allocation/expansion
check, not the accepted historical payload. No shared stamping redesign is required.

Preflight read retained maximums/distributions without dumping private text. If
retained definitions exceed candidates, adjust reviewed limits or provide an
explicit remediation plan before enabling enforcement. A scalar edit must not
force full receipt duplication of large unchanged children. Size/cost measurement
must include receipt indexes and PostgreSQL WAL, not only JSON payloads.

The finite epoch contract in 06 is the P02b retention protocol. Its proposed pilot
values are 24-hour admission epochs, a seven-day full acknowledgement/deduplication
horizon after commit, and a 30-second mutation deadline, all subject to retained-data,
supported client recovery and duration review before rollout. Persist each epoch's
issued parameters; option changes cannot shorten an already advertised horizon.
Initially full and compact retention are equal. There is no permanent per-operation
tombstone requirement: retire only eligible contiguous closed epochs, commit their
watermark before bounded resumable receipt deletion, and retain the watermark for
the dataset lifetime. Never infer operation age from its GUID.

An intermediate development commit may retain all receipts while closure/cleanup is
being implemented, but it does not complete P02b or justify long-running release.
An expired detail lookup returns `projects.operation-detail-expired`; a retired address
returns an explicit operation-expired outcome. Neither permits a new effect or claims
that the original operation did not commit. A destructive restore must perform the
managed offline namespace reactivation in 06 after restored files/SQL are in place and
before writers reopen; restored matching activation records alone cannot prove freshness.
Logical transfer preserves the target namespace and imports receipts only as history.

## Routes and migration matrix

Use explicit body expectations for the pilot. Do not label dashboard/history
responses with a strong ETag derived only from the definition revision.

| Route / adapter | P02a | Final P02b behavior |
| --- | --- | --- |
| Existing `GET /api/projects/{id}` (`GetProjectEditor`) | Return coherent editor DTO with `expectedDefinition`; old missing-template behavior can remain only on this compatibility route | Same compatibility read; protected clients use dedicated definition read |
| Existing `POST /api/projects` (`SaveProject`) | Existing ID requires original `expectedDefinition`; returns GUID; no operation retry promise | Optional compatibility alias requires an operation field/envelope and trusted invocation and uses protected writer; unsupported old bodies fail explicitly. If alias is not converted, reject writes with protocol-required, do not run P02a as a permanent bypass |
| New `GET /api/projects/{id}/definition` | Do not advertise protected-write completion | Coherent `ProjectDefinitionSnapshot` plus current operation namespace, Projects epoch and deadlines; missing ID 404 |
| New `GET /api/projects/definition-write-context` | Not required for CAS checkpoint | Supported request/result versions, current namespace, caller binding precondition, Projects epoch/admission deadline, replay horizon and enforced size limits; appropriate current read/write capability, no trusted actor or secret in response |
| New `POST /api/projects/definitions` | Not enabled | Required operation + create command; 201 accepted result and target Location; exact duplicate 200 accepted replay |
| New `PUT /api/projects/{id}/definition` | Not enabled | Required operation + replacement command; 200 accepted/replayed result |
| New `PUT /api/projects/{id}/definition/scalars` | Not enabled | Required operation + scalar command; 200 scalar accepted/replayed result; children preserved |
| New `GET /api/projects/definition-operations/{namespaceId}/{epoch}/{operationId}` | Not enabled | Required bounded callerBinding query precondition; current caller-scoped lookup under current read permission; 200 Found, NotObserved(Open/Draining) or authoritative ClosedAbsent; 410 expired evidence; explicit dataset/caller mismatch or detail-unavailable. Missing open-epoch evidence does not authorize a new identity or prove rollback |
| Existing Project Structure project-update HTTP/tool | Require original expectation and call scalar owner; preserve source/claim checks | Also require stable operation identity and trusted invocation; expose accepted scalar result/recovery instead of treating a later ProjectSummary as the ack |
| Native editor | Capture original expectation; exact ack; preserve conflict draft and unknown status | Create operation before dispatch; persist pending frozen submission through recovery; use exact lookup/replay |
| Direct C# service callers/tests | Original expectation mandatory on every existing update | Explicit trusted invocation for protected effects; retained contextless wrappers fail closed |
| Import/transfer | Fresh local definition revision; explicit locked maintenance scope | Fresh local revision/namespace rules; imported receipts are inert history, not replay authorization |

The existing POST alias choice is a deployment compatibility choice, not a safety
choice: either adapt it completely or refuse it. Prefer refusing old writes at
cutover and moving official clients to the new distinct routes; this avoids
accumulating a second overloaded wire envelope. The existing method signatures
can still exist for source compatibility with an explicit failure.

For known precommit rejection use the existing error envelope and mappings above.
Add 409 for operation payload mismatch/namespace refusal, 400 for a missing
or invalid operation address, 410 for expired/retired epoch, and 403/404 according
to existing access disclosure policy. Expiry rejects this new attempt but does not
resolve the prior historical outcome. An
unconfirmed owner result returns a documented 503 body containing operation and
recovery code when the transport is available; it is not a promise that a worker
will finish it. A timeout/disconnected response can leave the client with no body,
so recovery must already be possible from its retained operation reference.

Lookup does not receive a user-supplied caller ID. It queries only the active
trusted caller scope, then checks current disclosure permission. Receipt absence
or a revoked/deleted target must not leak another caller's target/definition.
If no safe detail disclosure is allowed, return the existing access refusal;
do not let a denial authorize repeating the effect.

### Exact current source perimeter

| File | Required change |
| --- | --- |
| `src/Modules/CanDoItAll.Modules.Projects.Contracts/ProjectPortfolioModels.cs` | Serialized `ExpectedDefinition`; existing scalar/child DTO mapping and legacy descriptions no longer promise token omission |
| `src/Modules/CanDoItAll.Modules.Projects.Contracts/ProjectEditorAcknowledgement.cs` | Mandatory accepted definition expectation, exact external identity, omitted-option reconciliation contract; retain submitted-index mappings |
| `src/Modules/CanDoItAll.Modules.Projects.Contracts/ProjectWriteAdmission.cs` | Lifecycle semantics unchanged |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectModels.cs` | Root revision mapping; facade delegation; coherent read mapping; no fresh-token fill; replace current full replacement loop with the writer |
| New top-level definition writer/contract files in Projects and Projects.Contracts | Pure normalization/identity rules; full/scalar/create execution; exact accepted values; no new partial |
| `src/Modules/CanDoItAll.Modules.Projects/ProjectCreationReceipt.cs` | Preserve compensation witness meaning; forward actual create through owner without substituting compensation for durable operation receipt |
| Projects owner context/configuration, canonical `AppDbContext` model and PostgreSql migrations | Matching application-managed token mapping, retained-data backfill, one migration lineage, later receipt schema |
| `src/Modules/CanDoItAll.Modules.Projects/DatabaseTransfer/ProjectsProfileTransferStore.cs` | Fresh target revisions and history/receipt policy under existing maintenance lock |
| `src/App/CanDoItAll.Web/Api/ProjectsApi.cs` | Coherent read/mandatory expectation and explicit error mappings; final protected endpoint registrations/response types |
| `src/App/CanDoItAll.Web/ProjectStructureAgentApi.cs` | Scalar update preconditions, protected invocation and later operation response |
| `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentContracts.cs` | Add original expectation to `ProjectStructureProjectSaveRequest`; final required operation metadata; create rejects incompatible expectation |
| `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentService.cs` | Split existing-project scalar update from `BuildProjectEditor`; create may map explicit empty children; current summary is separate readback |
| `src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs` | Original definition-read tool/result and unchanged expected definition through prepared invocation; no capture of fresh definition token at update dispatch |
| `src/UI/CanDoItAll.Projects.UI/ProjectEditorDraft.cs` | B/L/S/C and pending-operation behavior below |
| `src/Modules/CanDoItAll.Modules.Projects/Pages/ProjectsPage.razor` | Host conflict/read/reconcile/recovery actions, owner-generation guards, no renderer writes |
| `src/App/CanDoItAll.Web/Api/WebCurrentPrincipalResolver.cs` and composition adapters | Reuse current principal checks at adapter boundary; do not move Web types into Projects |

The registered tool currently calls `CaptureNodeMutationAdmissionAsync` immediately
before `SaveProjectAsync`. That is a lifecycle/source check, not the original
definition basis. Keep the necessary authority check but require the unchanged
definition expectation from the earlier content read. Add an explicit
`project_structure_project_definition_get` tool/read response if the existing
project read cannot return a coherent definition and its expectation. Project list
summaries alone are insufficient. Prepared tool identity must include the frozen
request expectation; no prompt rule can repair a missing contract member.

## Native editor changes

Retain the current editor's `EditContext`, field revisions, row references,
submission capture before asynchronous work, and generation guards. They already
protect typing after dispatch and retired-editor completions.

1. On a coherent read, store immutable base B and its original expectation; clone
   editable L. `Capture` creates immutable S with the same expectation, frozen
   row order and original live-row references. Copy external identity intent too;
   current `Clone` omits those fields. Never capture only the stamp from a newer
   portfolio/agent refresh.
2. P02b allocates the operation when S is created and durably retains its caller
   binding, request interpretation version and frozen typed command before dispatch.
   Double submit reuses the admitted submission or is blocked;
   it does not allocate a second effect. Typing after dispatch still changes L.
3. Exact acceptance validates target/lifetime/submitted indices, moves B to the
   accepted state and moves the current expected revision to that accepted
   revision. Merge accepted normalized field values into L only when the current
   field still matches its submitted value and revision. Preserve post-dispatch
   typing and raw invalid date text. Map row IDs by captured row reference.
4. For an omitted blank option, if its captured live row still exists, clear its
   old persisted ID because that ID was deleted. Preserve text typed into that
   row after dispatch so a subsequent save intentionally creates a new row. Do
   not reinsert a row the user removed while saving.
5. A conflict records a typed conflict state, keeps B/L/S, and permits a separate
   authorized current read C. Viewing C does not change L's expectation. Initial
   actions are inspect current, explicitly discard/reload, or manually reconcile.
   There is no force-save, automatic merge, or automatic stale retry.
6. Manual reconcile creates an explicit new editing session based on C, retaining
   the old draft for comparison/copy until the operator finishes that transition.
   It must not simply transplant C's token onto the old full L payload. The new
   submission uses a new operation. A conflict during reconciliation repeats the
   same safe workflow.
7. Unconfirmed state retains the exact frozen pending operation, including namespace,
   caller binding, request version and epoch. Lookup Found
   acknowledges that exact submission; NotObserved stays unresolved even after a
   later rejected attempt and may offer
   exact retry only in P02b when the original address is still admissible. Closed,
   fully retained absence may resolve uncertainty explicitly; expired or retired
   epochs preserve historical uncertainty and require review/recovery. No automatic context refresh
   or fresh-ID resubmission is allowed. A modified L cannot reuse the pending operation's ID.
   Current readback remains separately labelled current state.
8. Save-and-open, starter object seeding and navigation follow confirmed acceptance.
   Definition receipts do not prove seed completion. Preserve the existing
   separate `SeedOutcomeUnknown`, retired-target checks and per-target mutation
   blocking. Late lookup/replay completion cannot reopen a closed/new editor.

Native P02b release requires browser/circuit restart recovery of the frozen submission
and exact accepted result. The durable versioned record contains caller binding,
operation, command kind/version, frozen typed command, pending/uncertain/accepted state,
record revision and optional exact accepted bytes. It excludes EditContext,
FieldIdentifier dictionaries, live row references, authority objects and credentials.
Use per-operation keys, persist before dispatch, and visibly block dispatch on storage
failure. Do not evict unresolved records or auto-submit on startup/storage events.
Persist Accepted before local merge when possible; storage/merge failures remain
separate from the confirmed outcome. Clear only the matching operation/record revision.
Authorize the current binding before displaying stored content after account change.

The pilot uses the existing same-origin browser storage primitives with explicit
bounded plaintext retention and inspect/discard controls; this creates no isolation
between people sharing a browser profile. The restart promise does not preserve typing
performed after dispatch or raw incomplete date fragments. Those remain separate
unsubmitted state unless a later explicit draft-checkpoint feature is implemented.
No new editor history/checkpoint feature is required for this slice.

P02a conflict preservation and original-stamp acknowledgement are required for a
usable native editor. P02b recovery can be added later without discarding this
state machine. Conflict/recovery rendering stays in Projects.UI; effects stay in
the Projects host. Shared component discovery is required if implementation adds
a reusable comparison/recovery surface.

## SPA: concrete read-only assessment and future change scope

The sibling `../CanDoItAll.SPA` has not been edited by this work.
Its current serializers discard protection fields, and its save handler reloads
the whole editor after save. It cannot remain an accepted tokenless writer.

| Sibling file | Required scoped work before re-enabling protected editing |
| --- | --- |
| `packages/api-client/src/models/models.ts` | Regenerate expectation, snapshot, full/scalar/create command, operation, exact ack and outcome types from the protected API |
| `packages/api-client/src/models/internal/serializers.ts` | Regenerate and round-trip-test every nested expectation/operation field; current explicit serialization loses unknown fields |
| `packages/api-client/src/api/canDoItAllClientOperations.ts` | New definition/read/write/lookup routes, typed HTTP errors, accepted receipt responses rather than GUID-only save |
| `packages/api-client/src/canDoItAllClient.ts` | Expose those generated operations without unwrapping away recovery metadata |
| `packages/app/src/pages/projects/api.ts` | Preserve stable error codes, HTTP status, operation reference and accepted result; current `Error(message)` loses that structure |
| `packages/app/src/pages/projects/ProjectOverviewPage.vue` | Original B/L/S/C state; freeze payload/arrays before dispatch; preserve dirty draft and pending operation; explicit conflict/reload; exact ack; hierarchy refresh cannot reset definition draft |
| `packages/app/src/pages/projects/ProjectNewPage.vue` | Admit operation namespace/epoch and retain the full create address before dispatch; recover same target instead of duplicate create on a lost GUID response |

This requires a separate sibling-repository implementation/validation scope.
Until it is done, backend cutover must explicitly refuse the old write contract.
Read-only API clients may continue; a release must describe the temporary editing
restriction instead of silently accepting a concurrency bypass.

## Scoped proof and P02a work card

No test has been executed for this design document. The following are planned
cases, not discovered/executed counts. Derive counts from the final source,
confirm build-backed discovery, then execute the same filter/configuration.

| Layer / current suite anchor | Cases that must distinguish correct implementation from a shallow token addition |
| --- | --- |
| Direct pure writer-rule tests, new small suite | Full versus scalar intent; missing/contradictory expectation; duplicate/foreign/unknown child identity; blank persisted option deletion; ordered submitted-index mapping; normalization and undefined enum rejection; bounded copying/size rejection |
| `ProjectsServiceIntegrationTests`, `ProjectsOwnerPersistenceTests` | Existing update without expectation rejects; first reader wins; second original full/scalar request conflicts without mutation; child-only save advances root; accepted no-op advances; old lifetime/profile fails; accepted values captured before later competing save |
| PostgreSQL owner tests using explicit barriers | Root read paused before child read while a competitor commits remains coherent; overlapping update conditional predicate; advisory wait with old snapshot; child rollback on root failure; two independent hosts/connections, not just process stripes |
| `ProjectStructureStaleEditHttpTests`, runtime tool round-trip tests | Actual scalar HTTP/tool update preserves seeded children; original stale expectation rejects; tool metadata includes original token; read/list/prepare never silently substitutes a later definition revision |
| Real Projects HTTP tests | Serialization retains nested admission/revision; route/body mismatch; missing expectation; missing target 404 on new read; existing conflict codes; explicit legacy rejection after final cutover |
| `ProjectEditorSurfaceTests`, `ProjectsEditorMutationTests`, `ProjectsPageTests` | Conflict leaves draft/EditContext/raw invalid input; accepted stamp advances while later typing remains; reordered/removed child association; clear then retype option clears deleted identity; retired completion ignored; separate seed failure |
| P02b receipt/HTTP tests | Same complete address and command before/after commit/restart gives one effect and exact old ack; changed body/target/basis rejects; duplicate losing snapshot reconciles fresh; open/secure/native caller modes; current historical disclosure; response-loss lookup; NotObserved is not rollback; closed/retired epoch never reexecutes |
| P02b bounds/migration tests | Candidate boundaries including escaped UTF-8/result expansion; retained-data preflight; owner/canonical parity; fresh import revisions; receipt transfer/restore namespace fence; projection failure rolls back definition and receipt |
| Native browser, two independent contexts | A/B load; B edits children; A conflicts with dirty text intact; explicit current/reconcile; typing/reordering during delayed acknowledgement; close/navigation then late completion; operation lookup after a deliberately lost response |

P02a implementation card, in dependency order:

1. Add the expectation/intent/ack vocabulary and root concurrency mapping; derive
   and validate canonical migration/backfill plus owner-model parity. Keep
   lifecycle admission meaning unchanged.
2. Extract the definition owner and pure normalization/identity functions; retain
   public facade signatures. Add the root original-value predicate and explicit
   root advancement even for child-only/no-change saves.
3. Implement coherent existing-project read and map the original expectation into
   the existing DTO. No readback after successful save is used as the ack.
4. Route full native/API saves and scalar Workbench HTTP/tools through the right
   shape. Require original expectations on every enabled existing-object update.
   Convert callers/tests to obtain an actual original read; do not handcraft the
   owner's current revision immediately before the write.
5. Update draft capture/ack/conflict behavior and exact error mapping. Test real
   caller serialization, stale outcomes and scalar child preservation.
6. Build affected production projects, use focused discovered tests, isolated
   PostgreSQL race/coherence proof, native UI proof for changed interactions,
   and final portability enforcement. Record exactly which final requirements
   remain for P02b; do not close the pilot at this checkpoint.

Keep migration, receipt, projection and complete caller conversion checkpoints
reviewable rather than mixing unrelated owners. P02a entry does not require a
speculative global operation framework. P02b must complete trusted context,
activation namespace, capacity preflight, atomic receipt/projection, recovery and
retention before the protected feature is declared complete. These are real
dependencies; a nullable parameter, generated ID or documentation warning cannot
replace them.
