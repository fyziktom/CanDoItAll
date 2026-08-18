# CP0 validator results

All commands ran from `codex/bundles/Simple-Llm-Chats-Hardening-Sse` after the SB00 records were
materialized.

| Command | Exit | Result |
|---|---:|---|
| `python scripts/validate_bundle.py --bundle-root . --stage executing` | 0 | 14 subbundles and 35 requirements passed. |
| `python scripts/check_traceability.py --bundle-root .` | 0 | 35 requirements and 17 findings passed traceability. |
| `python scripts/check_test_policy.py --bundle-root .` | 0 | Test-policy validation passed. |
| `python scripts/check_architecture_boundaries.py --repo-root C:\repositories\CanDoItAll` | 0 | Architecture boundary check passed. |

CP0 decision: Ready; unlock SB01.
