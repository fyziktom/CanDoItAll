# PostgreSQL and real-host API proof

All commands ran from `C:\repositories\CanDoItAll` against ephemeral local PostgreSQL.

| Command | Exit | Result |
|---|---:|---|
| Focused `FullyQualifiedName~LlmChatOperationDispatchClaimIntegrationTests` | 0 | 2 passed, 0 failed, 0 skipped |
| Focused `FullyQualifiedName~Request_lifetime_ends_before_provider_completion_and_does_not_cancel_durable_execution` | 0 | 1 passed, 0 failed, 0 skipped |

The first slice creates two independent `ServiceProvider` roots sharing the same database. Exactly one
claim succeeds. Cancellation committed through a second context is observed by the current owner's
database heartbeat store.

The real-host slice blocks the provider, proves the API returns `202 Accepted` first, cancels the
request token, releases the provider, and polls the durable operation to `Succeeded`. One initial run
used stale compiled output and returned 500; after rebuilding the affected Integration project, the
unchanged test passed. Sandbox-only application-data/build-output restrictions were rerun unchanged
outside the sandbox.
