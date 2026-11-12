# External Agent provisioning concurrency

`GET`, `PUT` and `DELETE /api/agents/by-external-key/{namespace}/{key}` resolve an
external identity inside the active organization workspace. Read the Agent editor
through `/api/agents/{agentId}` when preparing a full replacement. A provisioning
PUT is a complete configuration, including omitted members' defaults, not a patch.

GET returns the actual Agent's current archive status, modification time and opaque
64-character hexadecimal `configurationVersion`, also quoted in `ETag`. The response
uses `Cache-Control: no-store`. The version binds external identity, actual Agent
identity, modification revision, configuration and original workspace profile,
generation and scope. Ordinary Agent edits and separate decision-policy edits
invalidate it; changing data away and back also invalidates it. A binding's cached
last-provisioning metadata is not the current precondition.

Create without `If-Match`. Update or archive with the version from the original
reviewed read. Missing or stale versions return 412 without that mutation's effect.
Read current state and explicitly decide what to submit after a conflict; do not
attach a new version automatically to an old body. A profile context change also
returns 412 (`agents.external-key-context-changed`). New writes using old content-only
versions require a current read. Package-import bindings remain readable.

Every write requires `Idempotency-Key`. Keep the complete captured request,
including original body and `If-Match`, with that key until the outcome is known.
An identical retained request returns its historical receipt with `replayed: true`;
a different request under that key returns 409. The historical version is not
refreshed from current data. Replaying a receipt after later changes does not prove
that its configuration remains current. The existing history retains only 2048
operations per workspace; durable retention and namespace restoration qualification
remain open. Do not resolve an uncertain older operation by issuing a new key.

An unchanged Agent retains its modification revision. A new idempotency intent still
stores a receipt and updates historical binding metadata; catalog-wide revisions may
therefore advance. Ordinary provisioning preserves the separately owned Agent
decision-data policy. Change that policy through its dedicated owner/API.

The owner captures the normalized editor, including nested mutable access settings,
before asynchronous storage work and uses it for both fingerprint and persistence.
It uses catalog-only publication; execution journals are outside this operation's
dependencies. Original-byte publication conflicts return 409. Uncertain publication
or cleanup failures must not expose a prepared receipt as accepted.

CRM directory refresh follows a confirmed catalog write and carries the original
workspace identity. A refresh for another profile, scope or generation is refused
before projection writes. A refresh failure retains the acknowledged provisioning
receipt for reconciliation. Replay does not repeat the refresh. The catalog and CRM
projection remain separate commits. Profile checks supplement existing storage
fences; they are not a held global profile-activation lease.

The broader implementation and remaining runtime/retention gates are tracked in
[the concurrent mutations bundle](../codex/bundles/concurrent-mutations/README.md).
