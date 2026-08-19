# Profile lifecycle and security

## Runtime identity

The operation captures `LlmChatRuntimeIdentity` from the existing `IDatabaseRuntimeState`:

- active profile ID;
- active fingerprint;
- generation.

Do not persist credentials or connection strings in the operation.

## Operation lease

`ILlmChatRuntimeLeaseFactory` returns a disposable operation lease that:

- captures current identity;
- links cancellation to `IDatabaseSwitchNotificationService`;
- exposes `EnsureCurrent()` and cancellation token;
- fails when no active profile exists;
- never silently refreshes to a newer profile.


## Switch/commit synchronization

A generation check immediately before `SaveChanges` is necessary but may not be a complete concurrency
barrier by itself. SB00 must inspect the canonical profile activation/restart flow.

The accepted implementation must prove one of these:

1. the existing switch lifecycle cancels and drains active scoped operations before publishing the new
   generation and replacing the runtime; or
2. LLM Chats registers a narrow active-operation drain participant with that canonical lifecycle; or
3. the transcript store and profile switch share a proven synchronization primitive that prevents a
   commit from racing across generation publication.

A module-private notification received only after the switch is not sufficient evidence. Add a
deterministic race test that pauses immediately before transcript commit, triggers the profile switch,
and proves that the assistant entry does not commit.

## Mandatory fence points

Check the lease:

1. before reading definition/conversation state;
2. before operation admission;
3. before provider resolution;
4. immediately before provider dispatch;
5. immediately after provider dispatch;
6. before every transcript store mutation;
7. before operation finalization.

A profile change during inference must cancel or fail the turn and prevent assistant/conversation
completion in the stale profile. A narrowly defined immutable usage-audit write may still be attributed
to the originating profile when billable provider work already occurred; it must never make the turn
successful.

## Access and ownership decision at SB00

The repository currently supports a trusted local host and optional remote bearer authorization.
SB00 must inventory the canonical organization and authenticated-subject identities.

Rules:

- reuse an existing canonical organization/subject identifier if present;
- do not invent a second user directory;
- if no canonical per-user identity exists, definitions and conversations are profile-local in this
  bundle and the limitation is documented;
- all HTTP routes still use the existing API authorization convention;
- future external chatbot visitors are not represented as local users.

## HTTP input safety

The API accepts stable IDs and validated product DTOs, never:

- credentials;
- endpoints;
- complete `ProviderProfile` payloads;
- provider SDK objects;
- arbitrary local file paths;
- raw unvalidated provider model-setting JSON.

## Error safety

Public errors contain:

- stable error code;
- HTTP status;
- resource/operation IDs safe to expose;
- retryability where useful.

They do not contain transcript text, system prompts, provider payloads, credentials, connection strings,
or raw exception messages.
