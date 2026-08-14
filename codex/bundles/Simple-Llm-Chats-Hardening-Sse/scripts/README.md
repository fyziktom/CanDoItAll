# Bundle scripts

| Script | Purpose | When |
|---|---|---|
| `validate_bundle.py` | Structure, subbundles, JSON, acceptance and checkpoint contract | preparation and closure |
| `check_traceability.py` | Requirement/finding ownership and closure coverage | every checkpoint |
| `check_test_policy.py` | Command-budget and forbidden broad-lane enforcement | every subbundle/checkpoint |
| `check_architecture_boundaries.py` | Product/provider/Web dependency guards | after source changes |
| `check_sse_contract.py` | Streaming/SSE ownership and required source markers | SB07 onward |
| `generate_checksums.py` | Regenerate bundle SHA-256 list after intentional bundle edits | bundle maintenance only |

All scripts use only the Python standard library and are intended to run on Windows, Linux and macOS.
Source comments and diagnostics are in English.
