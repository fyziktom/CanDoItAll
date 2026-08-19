# SB09 Semantic Invariants

## Durable identity and follow semantics

- A UI remount follows `SelectedConversation.ActiveOperationId`; it never invents or redispatches an operation.
- Event cursors advance monotonically through the pure projection reducer.
- Duplicate cursors do not append duplicate text.
- A retention or cursor gap discards transient partial text before authoritative refresh.
- Terminal and recovery-required evidence trigger authoritative operation/transcript refresh.

## Transient presentation

- At most one transient Assistant message is rendered for the active operation.
- Transient user/Assistant messages are presentation-only and never become canonical transcript entries locally.
- Canonical transcript state replaces transient projection after authoritative refresh.

## Cancellation and recovery

- Component navigation, disposal, session disposal, and window close cancel only local following.
- Only the explicit Cancel action invokes durable cancellation.
- Reconcile requires Manage authorization and `RecoveryRequired` state.
- Abandon requires Execute authorization, successful reconciliation evidence, and exact selected-conversation/active-operation identity.

## Runtime profile fencing

- A profile-lifetime change ends the old follower, clears old-profile projection, and reloads the workspace under the new profile.
- Profile changes never implicitly cancel the durable operation.
- Old-profile events cannot be rendered after the lifetime fence trips.

## Safety

- UI errors cross only the sanitized `LlmChatUiFailure` boundary.
- Logs never include prompt/message content, credentials, provider bodies, or request fingerprints.
- The route and navigation remain inactive until SB10.
