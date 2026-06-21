# SB33 Semantic Invariants

- Provider quota, credit, and billing exhaustion must be classified explicitly and shown as an actionable provider account problem.
- Provider details included in user-facing failure text must be redacted and bounded in length.
- Rate limiting must remain distinct from quota or credit exhaustion.
- Cancellation semantics must not be converted into provider failure semantics.
- `.NET runtime` launch actions must be backed by typed metadata or concrete recoverable command evidence; vague notes must not create a fake run action.
- Relative `.NET` project paths are only valid when there is a resolved working directory to anchor them.
- Runtime command writeback must be considered incomplete when the written node cannot pass `ProjectStructureRuntimeLauncher.Resolve`.
- Chat session list/create/rename and latest-run bootstrap should use split chat/session/run projections when available.
- The legacy full-document chat path must remain available for stores that do not implement the split session interfaces.
- Opening contextual agent chat should make the window visible immediately and show busy state while backend session/workspace data loads.

