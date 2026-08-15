# Validator results

All commands ran from `C:\repositories\CanDoItAll`.

| Command | Exit | Result |
|---|---:|---|
| `python codex\bundles\Simple-Llm-Chats-Hardening-Sse\scripts\validate_bundle.py --bundle-root codex\bundles\Simple-Llm-Chats-Hardening-Sse --stage executing` | 0 | 14 subbundles, 35 requirements |
| `python codex\bundles\Simple-Llm-Chats-Hardening-Sse\scripts\check_traceability.py --bundle-root codex\bundles\Simple-Llm-Chats-Hardening-Sse` | 0 | 35 requirements, 17 findings |
| `python codex\bundles\Simple-Llm-Chats-Hardening-Sse\scripts\check_test_policy.py --bundle-root codex\bundles\Simple-Llm-Chats-Hardening-Sse` | 0 | Test policy passed |
| `python codex\bundles\Simple-Llm-Chats-Hardening-Sse\scripts\check_architecture_boundaries.py --repo-root .` | 0 | Architecture boundaries passed |

## Manual subbundle-validator result

Status: Pass.

- prerequisites: SB03 proof remains trusted and current;
- scope/non-goals: no streaming, SSE, UI, shared-component, broker, or LISTEN/NOTIFY work entered SB04;
- governed proof: immutable implementation commit, historical negative, direct owner tests,
  two-root PostgreSQL proof, real-host request-disconnect proof, source assertions, and architecture
  snapshot exist;
- dependent-flow trust: SB05 can replace transcript materialization without bypassing the durable
  dispatcher identity or exact admitted-turn resume contract;
- reopen rule: an inline provider invocation, unfenced owner write, local-registry liveness inference,
  or automatic post-dispatch reclaim reopens SB04 and relocks downstream proof.

## Architecture review gate

Status: Pass. Application owns lease/dispatch/execution decisions, persistence owns atomic database
adapters, and composition owns only hosted lifetime. The prior request-owned call path is unreachable;
there is no new cycle, forbidden dependency, public bypass, or production partial expansion.
