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

- prerequisites: SB02 proof remains trusted and current;
- scope/non-goals: no dispatcher, streaming, SSE, UI, or shared-component work entered SB03;
- governed proof: exact implementation commit, historical negative proof, direct owner/fence tests,
  real-host PostgreSQL retained-evidence proof, source assertions, and architecture snapshot exist;
- dependent-flow trust: SB04 can dispatch behind the same captured runtime identity and atomic commit fence;
- reopen rule: a public service bypass, changed switch ordering, or scope retained after terminal closure
  reopens SB03 and relocks downstream proof.

## Architecture review gate

Status: Pass. The final shape has one application scope owner, internal interface decorators, one
persistence commit adapter, and the shared switch/write serialization primitive. There is no new project
cycle, forbidden dependency, production partial expansion, or production unscoped interface path.
