# Validator results

All commands ran from `C:\repositories\CanDoItAll`.

| Command | Exit | Result |
|---|---:|---|
| `python codex\bundles\Simple-Llm-Chats-Hardening-Sse\scripts\validate_bundle.py --bundle-root codex\bundles\Simple-Llm-Chats-Hardening-Sse --stage executing` | 0 | 14 subbundles, 35 requirements |
| `python codex\bundles\Simple-Llm-Chats-Hardening-Sse\scripts\check_traceability.py --bundle-root codex\bundles\Simple-Llm-Chats-Hardening-Sse` | 0 | 35 requirements, 17 findings |
| `python codex\bundles\Simple-Llm-Chats-Hardening-Sse\scripts\check_test_policy.py --bundle-root codex\bundles\Simple-Llm-Chats-Hardening-Sse` | 0 | Test policy passed for recorded proof commands |
| `python codex\bundles\Simple-Llm-Chats-Hardening-Sse\scripts\check_architecture_boundaries.py --repo-root .` | 0 | Architecture boundaries passed |

## Manual subbundle-validator result

Status: Pass.

- prerequisites: SB04 proof remains trusted and current;
- scope/non-goals: no streaming, SSE, UI, shared-component, search, RAG, or summarization work entered SB05;
- governed proof: immutable implementation commit, historical negative, direct owner tests, real
  PostgreSQL large-transcript/query-count proof, source-removal assertions, and architecture snapshot exist;
- dependent-flow trust: SB06 can review the full backend chain without relying on a full-document turn path;
- reopen rule: a collection endpoint that reloads rows per item, an offset transcript path, or a turn
  path that loads a full EF document reopens SB05 and relocks CP1.

## Architecture review gate

Status: Pass. Product/application owns read contracts and context semantics, persistence owns bounded
SQL and atomic writes, and Web owns transport only. There is no second truth, new cycle, forbidden
dependency, public bypass, or production partial expansion.
