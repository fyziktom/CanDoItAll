# HR Simple Chat definition tools

This adapter exposes seven definition-administration capabilities to the existing managed HR Agent in an authorized interactive chat: summary search, creation options, exact settings disclosure, create, update, status, and scoped create-receipt lookup. The existing HR tools and ordinary Simple Chats remain separate application paths. Simple Chats does not acquire Agent tools or implicit chat context.

The adapter uses the Simple Chats application owner and its canonical profile lease. Each invocation verifies the persisted actor, chat, run, original approved scope, current source authority, current managed capability, and profile identity. Settings disclosure and mutations require the exact persisted approved proposal; current authorization can still deny a previously approved proposal. Registration never grants a capability.

The neutral Agent runtime persists an ordered tool batch and server-issued business intents before presenting approvals or dispatching serial tool calls. It retains the provider protocol needed to continue the original batch after restart. Typed request-scoped attachments cannot be recovered by this journal; affected administration tools report that limitation explicitly. Ordinary attachment handling continues through the existing chat path.

Create uses the owner's atomic create receipt. A lost file acknowledgement can recover the original definition ID and revision without creating another definition or overwriting later human edits. Update and status changes have no atomic owner receipt and require reconciliation after uncertain dispatch. Diagnostic provider call IDs never become business intent IDs.

The existing execution-run API offers `POST /api/agents/execution-runs/{executionRunId}/recover` for supported admitted runs. It uses the original run, input, plain transient context, source scope, and saved provider segment. Pending approvals continue through the existing approval API. Completed recovery returns the saved result.

`POST /api/agents/execution-runs/{executionRunId}/reconcile-cancellation` only reads approved owner receipts under current read authority. It never restarts provider or tool execution. A missing receipt remains uncertain because an earlier owner transaction may still commit. A later positive receipt updates retained evidence without replacing a newer chat turn.

When HTTP authorization is enabled, both recovery routes require the general `api` scope. Their public responses expose confirmed effect identity and uncertainty without returning the internal receipt protocol. HR authorization reads the current Agent and capability assignments from one canonical catalog snapshot; it does not call back through the workspace execution service.

Application composition installs this adapter together with the durable admission verifier and the Simple Chats owner receipt service. The complete PostgreSQL migration history remains the only schema authority. The adapter supplies its operation policy; the host's immutable policy catalog supplies capability metadata used by runtime, approvals and display.

Portable Agent packages retain redacted run history and results. Export and import remove the local admission journal and its provider checkpoint, including private prepared settings and original transient context. Imported history cannot resume the source workspace's admitted operation; recovery remains bound to the original workspace and profile.
