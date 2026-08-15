# Source and architecture guards

Implementation commit: `58265975e868731e25e39d4bf9109f6010d68127`

| Command | Exit | Result |
|---|---:|---|
| `python codex/bundles/Simple-Llm-Chats-Hardening-Sse/scripts/check_architecture_boundaries.py --repo-root .` | 0 | Implemented LLM Chat architecture boundary checks passed. |
| `python codex/bundles/Simple-Llm-Chats-Hardening-Sse/scripts/check_sse_contract.py --repo-root .` | 0 | Streaming/SSE source contract check passed. |
| `git diff --check` | 0 | No whitespace errors. |

The architecture guard enforces product/persistence dependency direction, no service location or
production partial expansion, no global generic-conversation activation, no LLM Chat Razor/UI diff,
no dormant deployment fields, server-owned HTTP origin, shared-context transcript persistence,
background dispatch, post-commit notification-only behavior, and reuse of the one shared SSE writer.

No production C# source changed in SB12, so the successful SB11 CodeAnalytics snapshot
`snap-20260815080824-3b5bd776` remains the current dependency/cycle proof. The strengthened source guard
adds executable assertions without claiming an architecture change.
