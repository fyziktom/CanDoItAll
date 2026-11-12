# Provider concurrent editing

The provider panel retains one draft and `EditContext` across sections, metadata refresh,
failed reads and comparison. An editable configuration read supplies its original token.
Save and Delete require that original at the database boundary; an absent explicit identity
requires the empty GUID observation. Reading a newer token immediately before a replacement
does not authorize that replacement.

The native writer captures its exact accepted editor before secondary notifications.
Acknowledgement reconciles submitted values, later local input and that accepted editor.
A later current read invalidates the original instead of supplying an acknowledgement.
Comparison combines name, notes and tag membership; connection, secret binding, runtime
settings, models, prices and stored extensions form one configuration choice. Applying a
comparison is local. Save is a separate action. Invalid JSON, numeric input and unfinished
tags remain with their draft. Private extension values are omitted from comparison summaries.

The interactive panel retains its original actor and dataset admission. Protected reads
authorize before and after payloads. Native configuration and diagnostic writes recheck
that admission before SQL and before committing an owned transaction. Target retirement
and actor changes cannot rebind an in-flight editor. Retained unresolved attempts cannot
be reopened under another actor or dataset activation.

## Diagnostics and remote model maintenance

Health probes capture a coherent native configuration revision and fingerprint before
external work. Their persistence changes only health status and check time; it cannot
change the editable token or import discovered models. Metadata hints do not invalidate
the form's configuration original. Discovery into an editable draft remains a separate
action followed by review and Save.

Remote model maintenance captures its configuration original before external execution.
Its default-model update remains conditional on that original. A peer edit is preserved.
If the remote operation completed but local persistence conflicted, failed or needs
reconciliation, its result is retained with a typed persistence state. Repeating the
remote operation is not a reconciliation action. The HTTP boundary requires execution
and configuration-write access, retains the original HTTP actor and dataset, and checks
them again at native publication and before returning the protected result.

## HTTP outcomes and verification

`POST /api/agents/providers` accepts the original editor token. Conditional deletion uses
`DELETE /api/agents/providers/{id}?expectedConcurrencyToken={token}`. A stale or missing
original is a conflict. The editor read is `GET /api/agents/providers/{id}/editor`.

`POST /api/agents/providers/{id}/ollama-modelfile` returns the remote result on ordinary
success. Partial completion returns the remote result and `configurationState`, with
`remoteOperationCompleted=true` and `automaticReplaySafe=false`. Conflict, rejection and
unconfirmed persistence use HTTP 409; committed persistence with a secondary warning uses
HTTP 202. These responses are not cached.

`POST /api/agents/providers/mutations/verify` accepts the server-issued attempt unchanged.
The existing Data Protection key ring attests the dataset address, activation, attempt
identity, kind, original/intended tokens and submission fingerprint. Missing, modified or
foreign attestations remain unconfirmed. UI dispatch correlation is separate from the
native receipt identity. An unchanged expected configuration token cannot prove the
outcome of diagnostic-only metadata; absence cannot prove a create or delete outcome.
No new durable receipt table is introduced.

A diagnostic-only uncertainty can be explicitly acknowledged after reading current
metadata. It remains recorded as unconfirmed, does not repeat the probe, and does not adopt
another configuration token. Configuration uncertainty continues to require its recovery
or explicit review path.

When exact accepted values are unavailable, the editor can read the current saved version
for an explicit recovery review. It does not invent an original snapshot or infer the
earlier commit. Keeping the complete draft adopts only that reviewed current identity and
version. Raw invalid fields, pending tags and the existing EditContext survive. A separate
Save is conditional on that reviewed version. Unconfirmed attempt metadata remains in a
bounded local audit; a missing or unavailable current provider cannot enable a create retry.

The standalone file registry requires the actual workspace identity and binds its tokens
to the coordinated catalog revision, scope and activation. This conservatively invalidates
an editor after another catalog mutation, including diagnostic metadata. The main SQL owner
keeps diagnostic metadata separate from its editable token. Both Core and the main runtime
capture their diagnostic/model-maintenance observation before external work. Virtual seed
profiles must be saved before persistent diagnostics or model maintenance.

The file catalog records its accepted catalog at the primary publication boundary. Later
observer or workspace-index failure retains that exact accepted state as a committed
warning. Provider adapters preserve it without reading a later peer snapshot. SQL provider
and administration writers likewise retain known commits, attempt every secondary observer,
and publish targeted change hints even when another observer fails. Administration writes
carry the same original admission through SQL and transaction commit.

The list uses targeted metadata hints and explicit covering refresh because its native
read currently materializes provider bodies. It performs no body query per hint. A list
refresh does not replace an open editor or acknowledge an unreviewed configuration change.

## Shared source configuration and selection

Source creation uses a stable candidate identity with an explicit empty expected version.
Updating an existing identity requires its original version. Matching values on a later
read do not prove that an earlier create or update committed, and repeating a create for
an existing identity is a conflict.

The source editor retains its original values and EditContext. Name changes can be
compared independently; the instance URL, credential reference, enabled flag and private
network permission form one connection group with an explicit side choice. Unfinished
URLs remain private local input and require an explicit keep choice. Applying a review
is local; Save is a separate conditional operation.

Replacing a source's imported selection requires `SharedProviderSourceObservation` from
the reviewed source snapshot. It contains the original source version and immutable
import identity/version pairs. The owner checks these before credential resolution or
remote fetch and again under the source's serializable mutation scope. A peer retirement
or synchronization invalidates the old selection even when the source configuration token
did not change. The identity-only synchronization port refuses replacement requests.
Connection testing is a narrow intent against the current connection, captures its
native original before external work, and conditionally persists only that result.

Configuration and reconciliation writers recheck the original UI admission before SQL
and before the owned transaction commits. They publish targeted source metadata hints.
The dialog and refresh button keep the original actor and dataset across reads, writes
and recovery; protected reads authorize before and after returning their payload.

A successful synchronization returns the source/import observation captured at its own
commit. Catalog acknowledgment advances only to that accepted observation and preserves
any checkbox input received after dispatch, including a same-value event. A later read
can invalidate that original but cannot replace it. Selection recovery compares original,
local and current complete selections and requires an explicit choice before adopting
the reviewed current observation. A separate Apply selection action performs the write.

Source verification reads current state without establishing historical commit or
non-commit. A matching current postcondition is reported separately. An unresolved form
or catalog retains its draft for explicit review. A diagnostic or lifecycle uncertainty
can be acknowledged after a covering current read; newer hints invalidate that admission.
Acknowledged uncertainties remain in a bounded local audit and never replay a test,
synchronization, deletion or save. An absent create candidate remains unresolved.

Source lists currently materialize import metadata and model bodies. They therefore use
targeted metadata hints and visible explicit refresh rather than a body query per hint.
Only a successful read acknowledges its captured hint sequence; editor and catalog
originals are preserved independently.

The provider workspace keeps a retired import selected for audit through its
source-managed metadata snapshot and actual revision. Its native runtime authoring read
and generic configuration writes remain restricted. Reconciliation preserves that
metadata view so a known retirement can finish workspace delivery without repeating
the operation or selecting a different provider.

Shared source lists and sharing reads use a coherent native snapshot for their source,
import, provider and publication observations. Imported settings and retirement writes
hold the source mutation scope, validate both original import/provider versions, and
recheck the original UI admission before SQL and transaction commit. Their accepted
snapshot is captured before secondary observers. Retirement also publishes source
selection metadata. Transaction participation is released in the owning execution
context before secondary observers can begin another write.

Publication requires the original provider configuration version as well as the original
publication version (or observed absence for first publication). PostgreSQL locks the
matching provider revision through the command. The owner checks eligibility and original
UI admission and returns its own accepted configuration/publication values; it does not
read a later peer snapshot for acknowledgment. Low-level identity allocation is internal.
The main sharing child also retains its parent editor's original configuration revision.
Reading sharing status cannot adopt an unreviewed peer configuration for publication;
the operator first reviews that configuration in the parent editor.

The sharing panel, connection dialog and refresh button inherit the parent workspace's
original actor and dataset. Every protected read checks access before and after the
payload; every dispatch checks access before creating a pending attempt. Hints invalidate
review without querying bodies. Local import settings have explicit original/local/current
field review, retained invalid alias input and a separate conditional Save. Any newer
hint or input invalidates an open comparison. A native acknowledgment uses submitted/local/
accepted state, preserving input received while the command was in flight.

Target verification describes current values only and cannot produce a commit delivery.
An unconfirmed target write remains unconfirmed after matching or unchanged reads. The
operator may review local settings and explicitly acknowledge a covering current read;
the earlier attempt remains in bounded local audit and no command is repeated. Pending
import drafts survive child recreation under the same original recovery address.

The parent provider session activates Sharing on first use and keeps that editor mounted
in the existing client-rendered tab panels until the target changes. Switching sections
retains raw alias input, validation and EditContext without another body read. History
keeps its independent lazy lifetime. Publication observers emit targeted lifecycle hints;
the sharing panel invalidates its publication observation, while the parent configuration
draft keeps its unchanged original version. Neither hint performs a body query.
