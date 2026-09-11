# Workflow Provider Input Authority

A successful Workflow read does not grant permanent permission to send its result to an
LLM. The Workflow runtime retains evidence of the actual read and asks its owner to
recheck the original authority immediately before the provider call. This applies after
transforms, fan-out/fan-in and native checkpoint recovery.

The [provider-input admission service](../../src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowProviderInputAdmission.cs)
binds the request to the actual run, definition/version, node, occurrence, settings and
input. Registered executor metadata identifies the owner policy; a missing required
policy or missing evidence rejects the call. Provider-boundary checks consume all
retained protected reads for the run. They do not infer authority from prompt text,
public progress JSON or the newly selected project.

For Structure reads, the Workbench policy retains the original profile, source and
project lifetimes and validates them against current owner state. Revocation or
same-ID project recreation therefore blocks disclosure of an earlier successful read.
Owner validation batches project lifetimes and releases its leases before provider I/O.

## Credentialed HTTP

Credentialed HTTP reads retain the exact approval/request/operation and input binding.
The provider check can observe a consumed approval after restart; it does not acquire a
new dispatch lease or repeat the HTTP request. It requires the original live source,
current credential reference, matching approval and unexpired authorization. It cannot
claim that an unchanged secret reference identifies an unchanged secret value.

The secret remains transient inside the transport operation. Raw and formatted echoed
credential values, standard JSON string escaping and credential-bearing response headers
are redacted before results, downloaded text or provider input are released. This does
not promise detection of arbitrary encodings invented by a remote server. A credentialed
redirect does not automatically send a second request; its exact destination needs a
separate review and approval. The first request's possible external effect remains real.
Ordinary requests without credentials preserve their existing redirect behavior.

## Configured Workspace Files

File disclosure retains the actual file service's immutable workspace scope and the
owner-selected physical targets. Public display paths do not authorize a read or identify
its physical target. Current source/profile authority and physical containment are checked
before provider dispatch, including after transformations and native wait/resume.

List and tree reads use the file owner's existing shorthand normalization, including
`sources/**`, while retaining the original settings and invocation identity. Revalidation
checks the originally selected physical root and children; it does not list the directory again.

External aliases remain bound to their originally resolved physical target; removing or
rebinding a current alias cannot substitute a different location. Native absolute inputs
remain subject to the original SourceIngestion setting that permitted them. File metadata
must preserve native filenames, including literal Unix backslashes, while remaining absent
from public result JSON.

SourceIngestion rechecks its existing complete SHA-256, length and UTC modification-time
identity. Ordinary file list/stat/text results bind the actual returned result and selected
path; they do not introduce a full-file content snapshot or promise an unchanged directory
listing. Symbolic-link/reparse replacement and loss of the original path authority refuse
disclosure. The local alias registry is not a remote ACL service.

## Persistence And Recovery

Run admission atomically stores a declaration bound to the exact definition/source and reviewed
simulation plan with the run and Started event. Actual node completion binds the full
result digest and owner evidence to its original invocation. Both initial execution and
native resume await durable owner persistence before returning protected output to the
SDK. The final resume boundary enlists its event/evidence writes in the existing
checkpoint/request transaction.

The [Workflow evidence journal](../../src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.Runtime/WorkflowProviderDisclosureJournal.cs)
uses private `WorkflowEventKind.ProviderReadEvidence` rows in the existing Workflow
event table. Ordered parts, immutable headers and links to visible completion events
detect missing or altered partial evidence. Exact simulation steps use bounded chunks;
recovery cannot replace a missing reviewed simulation with real execution. This is
durable execution evidence, not a tamper-proof ledger against coordinated direct
database edits that erase every record of a completion.

Private metadata is excluded from public event lists/pages, History and runtime-evidence
projections. Public diagnostic truncation and redaction are presentation behavior; the
provider-input decision uses the separate complete evidence. Re-observation can link
another visible event only when its full original completion proof and evidence match.

## Deployment And Rollback

Deploy the producer, persistence codec, provider gate and every reader filter together.
Compiler contract v2 checkpoints require compatible readers. Genuine undeclared v1
checkpoints may continue only through the retained legacy contract; protected-read
authority cannot be reconstructed from old display events. History remains readable,
and a new execution requires an explicitly reviewed new run.

The schema-neutral `BindWorkflowProviderDisclosureHistory` migration records this
protocol boundary without changing tables or the complete canonical model. Its Down
locks the Workflow event table and refuses any retained private row, including orphaned
or malformed rows. Stop and drain current disclosure writers before downgrading; the
table lock cannot fence queued writes after commit. Downgrade is available before
private evidence exists.

Existing startup does not reject unknown later migration IDs after its baseline is
present. The migration marker therefore cannot prevent an older binary from starting.
Direct older-binary rollback after private evidence exists is unsupported. Any reviewed
backup restore must also reconcile completed external effects rather than replaying them
under new identities.
