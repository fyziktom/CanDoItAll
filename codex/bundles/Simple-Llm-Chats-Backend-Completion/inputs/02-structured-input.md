# Structured Input

## Requested Outcome

- Prepare a new implementation-ready bundle that completes the unfinished Simple LLM Chats hardening/SSE work, incorporates current workflow test discipline, and owns newly identified backend bugs and justified refactors.

## Constraints

- Do not implement product or test code during preparation.
- Keep the work backend-only; no UI integration or chat-component refactor.
- Retain the current stronger authorization, server-owned origin, durable dispatch, and profile/SSE behavior rather than reverting to predecessor contracts.
- Prefer focused validation during development and one broad stable gate only at a named frozen checkpoint.
- Use strongly typed contracts, explicit errors, bounded work, safe logs, and existing repository boundaries.

## Current-State Conclusion

- The predecessor package-feed blocker is stale and replaced by a pinned sibling-source graph.
- SB00-SB12 implementation claims are partly retained, but later code and test-topology changes reopen SB03/SB06/SB09/SB11-SB13 proof and invalidate CP1/CP2/FINAL.
- The successor starts from current source; it does not resume SB13's obsolete package-mode commands.

## Locked Decisions

- Ordinary LLM Chat execution remains independent from agent execution, tools, memory, processes, Projects, and workspaces.
- PostgreSQL remains canonical; the generic file store is not activated for the product.
- No new C# project or public abstraction layer is justified.
- Keep `MapLlmChatsApi` as the stable composition entry point, but split definition and conversation endpoint ownership into separate internal Web types/files.
- Exclude system messages from the read-scoped transcript before paging; add a manage-scoped definition editor projection for sensitive mutable configuration.
- Remove the external request fingerprint from operation responses; expose only bounded, sanitized invocation evidence required by the documented operation contract.
- `Cancel` and `Recover` are actions/status transitions, not operation kinds. Remove the unused `LlmChatOperationKind.Cancel` and `.Recover` values while preserving `SendTurn = 0` and treating any impossible persisted value as explicit corruption.
- Add a manage-scoped operation reconcile route. It may settle only outcomes proven by durable evidence and must never redispatch ambiguous post-dispatch work.
- Preserve a durable per-operation event high-water mark across event retention.
- Cleanup batch size means event rows, not operation count.
- Configure bounded worker concurrency, queued age, and total operation duration; database claims remain the durable queue and no in-memory shadow queue is introduced.
- Conversation creation remains explicitly non-idempotent in this bundle because there is no deployment-owned caller identity namespace. The existing warning remains part of the contract.
- Do not split `LlmChatConversationEngine` solely because of line count; revisit only if implementation exposes an independently testable second responsibility.

## Closure Interpretation

- Prepared means the bundle can be executed without guessing scope, ownership, proof, or dependency order.
- Completed means every work unit passed its declared gate at one final frozen source commit, the broad stable gate ran once for the named cross-cutting triggers, and the three-OS CI matrix agrees.
- Any production, migration, API, test-topology, build-graph, or workflow change after its owning checkpoint reopens the named downstream proof.
