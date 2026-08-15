# Static final guards

Candidate commit: `dea90cfd4cc77e60f1a7d07a2dc16d44165840f9`

| Command | Exit | Result |
|---|---:|---|
| `./tools/Validation/Test-Documentation.ps1` | 0 | 181 maintained Markdown files pass. |
| `python .../validate_bundle.py --stage executing` | 0 | 14 subbundles and 35 requirements pass. |
| `python .../check_traceability.py` | 0 | 35 requirements and 17 findings pass. |
| `python .../check_test_policy.py` | 0 | Test-policy validation passes; no SB13 broad command is recorded. |
| `python .../check_architecture_boundaries.py --repo-root .` | 0 | Implemented architecture boundary checks pass. |
| `python .../check_sse_contract.py --repo-root .` | 0 | Streaming/SSE source contract check passes. |
| verify every entry in `CHECKSUMS.sha256` | 0 | 251 bundle files match. |

These checks establish that the candidate and prior proof remain internally consistent. They do not
substitute for the unrun solution build/test, final pending-model command, or hosted matrix.
