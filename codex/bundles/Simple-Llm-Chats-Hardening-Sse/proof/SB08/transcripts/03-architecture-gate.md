# Architecture gate

CodeAnalytics snapshot: `snap-20260815060048-09276cd1`.

- scoped projects: `CanDoItAll.Modules.LlmChats` and
  `CanDoItAll.Modules.LlmChats.Persistence`;
- project direction: Persistence references product; product has no project reference back;
- cycles: 0;
- workspace diagnostics: 0;
- blocking errors: 0;
- error findings: 0;
- open questions: 0;
- partial-class expansion: none;
- Web/SSE dependency in product/persistence: none;
- old completed-only engine invocation path: removed.

The one warning is the 391-line pre-existing `LlmChatConversationEngine`. The SB08 change removes its
completed-only invocation method and adds only the required stream boundary. Splitting unrelated
definition/provider validation would be scope expansion rather than an SB08 correctness fix, so the
warning is recorded and accepted as nonblocking.

## Manual architecture review

Status: Pass.

- immutable event variants and repository ports are product-owned;
- EF owns row locking, mapping, queries, migration, retention delete, and transfer integration;
- the application pipeline owns bounded aggregation/coalescing and requires the execution lease;
- transcript finalization remains the sole canonical assistant-message owner;
- the local signal is profile/operation keyed and post-commit only, never a source of truth;
- direct tests target the journal/coalescer/pipeline and PostgreSQL repository rather than a facade;
- no new project, cycle, partial class, ambient transaction, nested DbContext, or Web coupling exists.
