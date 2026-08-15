# Validator results

Current source guards at implementation commit `4ec4d2694d980d52936b4679ae676a0624d5c6fb`:

| Command | Exit | Result |
|---|---:|---|
| `python .../check_architecture_boundaries.py --repo-root .` | 0 | Architecture boundary check passed. |
| `python .../check_sse_contract.py --repo-root .` | 0 | Streaming/SSE source contract check passed. |
| production partial/source-diff guards | 0 | No LLM Chat production partial and no SB11 production-source change. |
| CodeAnalytics scoped snapshot | 0 | `snap-20260815080824-3b5bd776`; zero cycles or blocking errors. |
| `python .../validate_bundle.py --stage executing` | 0 | Bundle validation passed: 14 subbundles, 35 requirements. |
| `python .../check_traceability.py` | 0 | Traceability passed: 35 requirements and 17 findings. |
| `python .../check_test_policy.py` | 0 | Test-policy validation passed. |

The same bundle, traceability, test-policy, architecture, and SSE validators are rerun after checksum
generation so the proof commit cannot depend on stale inventory.
