# Validator results

All automated commands ran from `C:\repositories\CanDoItAll` after the SB10 implementation commits and
proof updates.

| Command | Exit | Result |
|---|---:|---|
| `python codex\bundles\Simple-Llm-Chats-Hardening-Sse\scripts\validate_bundle.py --bundle-root codex\bundles\Simple-Llm-Chats-Hardening-Sse --stage executing` | 0 | Bundle validation passed: 14 subbundles, 35 requirements, stage=executing. |
| `python codex\bundles\Simple-Llm-Chats-Hardening-Sse\scripts\check_traceability.py --bundle-root codex\bundles\Simple-Llm-Chats-Hardening-Sse` | 0 | Traceability passed: 35 requirements and 17 findings. |
| `python codex\bundles\Simple-Llm-Chats-Hardening-Sse\scripts\check_test_policy.py --bundle-root codex\bundles\Simple-Llm-Chats-Hardening-Sse` | 0 | Test-policy validation passed. |
| `python codex\bundles\Simple-Llm-Chats-Hardening-Sse\scripts\check_architecture_boundaries.py --repo-root .` | 0 | Architecture boundary check passed. |
| `python codex\bundles\Simple-Llm-Chats-Hardening-Sse\scripts\check_sse_contract.py --repo-root .` | 0 | Streaming/SSE source contract check passed. |

## SB10 closure gate

Decision: Pass.

- server-owned origin is proven at the HTTP mapping, direct product command, and PostgreSQL row;
- exact scope policies are behaviorally enforced on auth-enabled hosts and absent on trusted-local hosts;
- standard bearer-header SSE works while query bearer tokens are rejected;
- versioned transport/OpenAPI and canonical links are public; domain/EF entities are not schemas;
- prompt, request-body, fingerprint-conflict, provider-secret, and raw-exception paths are redacted;
- future deployment concerns remain documented and absent from production schema/source;
- four filtered test commands and two builds remain within budget; no prohibited lane ran;
- CodeAnalytics and source guards report zero cycles, no forbidden direction, no partial expansion, and
  no dormant deployment model.

SB10 must be reopened, and SB11-SB13 revalidated, if origin becomes bindable, scopes collapse or broad
`api` implies LLM Chat access, SSE accepts query credentials, version/redaction contracts weaken, raw
exception logging returns, or deployment/participant concepts enter definitions/internal conversations.
