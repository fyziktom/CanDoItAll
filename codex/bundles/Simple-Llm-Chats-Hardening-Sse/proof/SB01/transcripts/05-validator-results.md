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

- prerequisites: SB00/CP0 proof remains trusted and current;
- scope/non-goals: no provider, Web API, UI, or shared-component implementation entered SB01;
- governed proof: manifest, semantic invariants, red/green PostgreSQL transcripts, source assertions,
  CodeAnalytics snapshot, implementation commit, and progression are present;
- dependent-flow trust: application ordering plus real shared-context PostgreSQL proof establishes the
  transaction boundary SB02 depends on;
- reopen rule: any later duplicate metadata writer, nested command context, or EF model mismatch reopens
  SB01 and relocks SB02 onward.
