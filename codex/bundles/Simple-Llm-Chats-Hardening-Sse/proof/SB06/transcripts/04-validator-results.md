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

- prerequisites: SB01-SB05 immutable manifests and proof artifacts are present and trusted;
- scope/non-goals: no streaming contract, provider stream, event journal, SSE, UI, or authorization
  implementation entered CP1;
- governed proof: current implementation commit, historical inline-path negative, current source
  assertions, current-head Unit/PostgreSQL/HTTP/transfer unions, model check, and architecture snapshot exist;
- dependent-flow trust: SB07 can add streaming without a competing inline operation lifetime;
- reopen rule: any change to transactions, reducer outcomes, profile scope, lease ownership, or bounded
  read semantics reopens CP1 and relocks SB07-SB13.

## Architecture review gate

Status: Pass. The hardened owner graph is explicit, provider invocation has one dispatcher entry point,
and no cycle, forbidden dependency, independent product transaction, or partial expansion remains.
