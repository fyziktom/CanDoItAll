# Generated images: preserve each actual phase

Sources S14/S15 describe an existing native path, not a proposed feature. The generic setup
currently obtains enabled image-purpose profiles, prepares prompt/model/size/quality/format,
stores a placeholder node, enqueues deferred completion and observes the result. WB4 extracts
its actual user-facing setup and status, without duplicating provider or storage engines.

## The accepted origin must reach the worker

Freeze the source project/profile/lifetime, parent/node occurrence, authenticated context,
operation, provider and model choice before provider catalog awaits or placeholder creation.
A live page ProjectId used after creating the placeholder is not its identity. The worker
creates its own service scope: prove it uses the captured native target rather than whichever
profile/lifetime is current when the item is dequeued. The replacement must also be matched
to the original placeholder/operation, not simply the same string NodeId.

At review the request carries OperationId/ProjectId/NodeId/kind and image settings, and the
worker writes Running metadata then media through Workbench. Audit actual mandatory/native
checks and all producers before changing it. Add narrow origin plumbing to the existing
request/processor/writer where required, using current project admission and occurrence
mechanisms. Do not build a generic durable-job framework or make UI-supplied labels authority.

Before any external generation, a retired original target must be refused without dispatch.
After generation already occurred, a retired target must refuse media attachment without
writing into its replacement. Keep the provider outcome observable; do not silently invoke
it again. This is a prerequisite to marking native generation safe, not a sandbox-only guard.

## Phase and failure table

| Phase | Required treatment |
|---|---|
| Catalog/settings validation | No implicit generation; distinguish unavailable data from an empty model catalog. |
| Placeholder persisted | Retain exact native project/node/storage identity and operation; not yet generation success. |
| Enqueue accepted | Show accepted queue state and original handle; not completed image bytes. |
| Provider accepted/completed | Preserve this fact separately from subsequent output persistence. |
| Native media replaced | Preserve the exact returned node/revision, image hash, content type and storage identity. |
| Readback/notification failure | Observe existing results; do not enqueue or generate again automatically. |

The existing queue is Channel.CreateBounded(64), single reader and asynchronous writer wait.
It is in-memory. Do not describe it as durable replay/restart recovery or install retry-on-start that
can duplicate requests. Test an interrupted owned host truthfully: retained placeholder
metadata can remain Queued/Running without a live task. Mark that limitation clearly or map
a necessary larger fix; do not display a fabricated completion or silently start another run.
This package does not require redesigning queue durability.

## Model identity and UI

The current Workbench image-provider option interpolates provider.DefaultModel directly.
For source-managed profiles that can expose an opaque route value to the operator. Use the
already established safe display metadata mapping, keeping native ID values unchanged. Prove
natively accepted defaults and explicitly stored nondefaults from source to two clients.
Do not populate imported choices from local OpenAI model heuristics or replace opaque IDs
with labels in a request. Disabled/retired source, wrong purpose, unavailable selected model
and missing credential remain explicit failures; no fallback to a personal provider.

Keep supported dimensions/quality/format and existing defaults. Explicit user generation
is an effect. Loading options, navigating tabs, opening a preview or refreshing a node cannot
trigger it. An image setup renderer registered through CanvasLib must remain owner/trust
validated; extract its presentation rather than copying a backend service into the leaf.

## Required native fixtures

Use deterministic image bytes behind the external provider boundary, actual queue/worker,
actual storage and canonical readback. Prove a normal image, bad/empty external response,
placeholder accepted followed by enqueue cancellation, accepted generation followed by
persistence refusal/lost acknowledgement, source retirement, deleted/recreated original
project or placeholder, stale observer and two simultaneous views. Decode the resulting image
and verify its hash through the authorized preview/download path. Assertions must distinguish
provider capture count from placeholder count. No real paid image request is authorized.
