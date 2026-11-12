# Agent chat concurrency

## Rename an existing thread

`POST /api/agents/{agentId}/chat-sessions/{chatSessionId}/rename` requires the title
originally displayed to the caller:

```json
{
  "title": "Reviewed title",
  "expectedTitle": "Original title"
}
```

The service compares `expectedTitle` ordinally with the current title under the selected
session's publication keys. Success returns the public session projection. A missing
expectation returns HTTP 428 with `chat.title_precondition_required`; a changed title
returns HTTP 409 with `chat.title_conflict`. Invalid identities or a blank requested title
return HTTP 400 with `chat.title_invalid`. Existing authorization requirements still apply.

Rename changes only the title and its monotonic update time. It preserves messages,
approval compatibility state and the latest execution identity. The requested title has
whitespace collapsed and is limited to 96 characters. An unchanged normalized title is a
no-op. The expected title is not normalized: it must describe the title the caller read.

After a conflict, retain the user's proposed title and explicitly reload the thread before
letting them reconsider the change. Do not automatically retry with the newly loaded title
as the expectation. A repeated successful request using its old expectation can return 409;
this endpoint does not provide an idempotency receipt. Conflict responses do not include
either the stored or proposed title.

Native chat hosts capture the original title from their loaded immutable session before
awaiting the rename. Their existing selection and lifetime checks prevent a late response
from updating another thread. The Process manager-chat host supplies its initial title when
creating a new session, so opening it no longer requires a second rename publication.
`GetOrCreateChatSessionAsync` rejects an initial title combined with an existing session ID.

## Publication ownership

Core defines the title-change condition. The native file owner acquires the selected agent
and session keys, reads the current session after acquisition, and applies the condition.
It reloads shared derived indexes under the short workspace guard. An unrelated execution
index revision therefore does not invalidate a new session or a title-only change.

Creation still observes absence of the chosen session identity. Both creation and rename
observe catalog, legacy-source and pending-recovery evidence. Pending publications affecting
the selected session or shared indexes remain barriers. Run, deletion and whole-workspace
witnesses retain their existing conditions. A prepared run publication can still conflict
with an intervening rename; the run owner must use its existing reconciliation behavior.

When an existing run publishes success, failure or cancellation from an older session
snapshot, the file store preserves the current title. Core applies the same rule in its
legacy document-store adapter. This prevents completion from undoing a successful rename.

These conditions do not create durable manager-chat identity across simultaneous browser
openings. They also do not remove global catalog contention or historical index costs. See
the [owner coverage ledger](architecture/concurrent-mutations/05-owner-coverage.md) for the
remaining concurrency work and the [P20 design](../codex/bundles/concurrent-mutations/plan/P20-chat-session-publication.md)
for this change's scope and validation requirements.
