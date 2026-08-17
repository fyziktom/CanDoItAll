# SB11 Semantic Invariants

## Neutral shell

- The shell merges typed contributor descriptors and never calls an Agent or Simple Chat backend directly.
- Available and Active are independent catalog axes; All, Agents, and Chats filter the selected axis without collapsing product actions.
- At most one conversation window is focused by the shell, while contributors retain the authoritative active lifecycle.

## Agent parity

- Agent context position, affinity, transcript history, approval-era behavior, and close decisions remain owned by the existing Agent coordinator/runtime.
- Follow-current binding fails closed when the surface publishes no valid Agent context; only the explicit Detach action changes that behavior.
- Hide/keep-active retains the Agent session and transcript; Stop chat terminates and removes it.

## Simple Chat lifecycle

- Simple Chats receive no ambient Agent or Project Structure context.
- Starting a definition, opening history, reopening an active conversation, archiving history, hiding a window, and cancelling an operation remain distinct actions.
- Hiding or refreshing does not cancel durable execution. Reopen follows the persisted active operation and canonical transcript without redispatching the turn.
- Audited streaming evidence uses a fresh DI scope and cannot concurrently use the chunk-persistence `DbContext`.

## Presentation and safety

- The unified overlay remains above the application workbar; the focused window and dialogs own bounded local scrolling.
- UI failures remain sanitized, profile-fenced gateways remain authoritative, and no prompt or credential content is logged as diagnostic state.
