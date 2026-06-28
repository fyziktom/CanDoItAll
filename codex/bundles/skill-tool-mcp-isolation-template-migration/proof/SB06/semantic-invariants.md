# SB06 Semantic Invariants

| Invariant | Result | Proof |
| --- | --- | --- |
| `SB06_INV_TEMPLATE_001` default capability pack materializes the expected canonical catalog keys without duplicates. | `Passed` | `passing-capability-template-seed-tests.txt` |
| `SB06_INV_TEMPLATE_002` representative configuration JSON is preserved for MCP, provider-native tool, workspace tool, inline skill, and RAG capabilities. | `Passed` | `passing-capability-template-seed-tests.txt` |
| `SB06_INV_TEMPLATE_003` invalid template input blocks materialization without fallback and returns structured duplicate-key and secret-binding diagnostics. | `Passed` | `failing-first-capability-template-seed-tests.txt`, `passing-capability-template-seed-tests.txt` |
| `SB06_INV_TEMPLATE_004` every `Templates/Agents/**/skills.json` capability assignment resolves against the template-backed catalog. | `Passed` | `passing-capability-template-seed-tests.txt` |
| `SB06_INV_SEED_001` the full sandbox seed document uses the template-backed capability catalog and all agent assignments resolve by ID/key. | `Passed` | `passing-capability-template-seed-tests.txt` |
| `SB06_INV_POLICY_001` the access policy template compiles to typed domain rules and unknown capability grants are rejected before seeding. | `Passed` | `passing-capability-template-seed-tests.txt` |
| `SB06_INV_POLICY_002` process `AllowedOperations` compile into typed operation-classification compatibility rules. | `Passed` | `passing-capability-template-seed-tests.txt` |
| `SB06_INV_STATIC_001` source assertions, anti-stub audit, static/performance scan, and file-size scan pass. | `Passed` | `source-assertions.txt`, `anti-stub-audit.txt`, `static-performance-scan.txt`, `file-size-scan.txt` |

## Notes

- The old seed helper methods remain in `SandboxWorkspaceSeedBuilder` for the SB11 cleanup gate, but the active seed catalog path now loads `Templates/Capabilities` and materializes from templates.
- Inline skill instruction bodies remain embedded seed assets and are referenced by template keys, avoiding duplicated long instruction text in JSON descriptors.
- The default access-policy file is loaded and compiled in SB06; runtime enforcement remains intentionally deferred to SB08-SB11.
