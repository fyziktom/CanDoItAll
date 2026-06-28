# SB07 Semantic Invariants

| Invariant | Proof | Status |
| --- | --- | --- |
| `SB07_INV_PARITY_001` default templates preserve behavior-critical keys, display metadata, stable GUID sources, tool runtime names, approval defaults, MCP allowlists, and default deny policies. | `passing-template-seed-hardening-tests.txt` | `Passed` |
| `SB07_INV_TEMPLATE_001` malformed packs fail with structured diagnostics for missing files, duplicate keys, raw headers, missing MCP allowlists, invalid default effects, invalid rule effects, ambiguous MCP tools, unknown implementation selectors, and missing capability grants. | `passing-template-seed-hardening-tests.txt` | `Passed` |
| Invalid template packs do not fall back to old hardcoded seed construction. | `SB07_INV_TEMPLATE_001` calls `SandboxWorkspaceSeedBuilder.Build(packRoot)` and asserts `CapabilityTemplatePackValidationException`. | `Passed` |
| `SB07_INV_POLICY_001` process `AllowedOperations` compile to typed behavior-equivalent policy rules: validation, mutation, runtime proof, script execution, external action, and write classes are denied unless granted by the operation contract. | `passing-template-seed-hardening-tests.txt` | `Passed` |
| `SB07_INV_POLICY_002` coarse agent workspace-tool flags compile to typed runtime-tool deny rules with parity to `AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed`. | `passing-template-seed-hardening-tests.txt` | `Passed` |
| `SB07_INV_POLICY_003` allow rules never grant capabilities absent from the candidate assignment set. | `passing-template-seed-hardening-tests.txt` | `Passed` |
| `SB07_INV_SEED_001` managed seed normalization is idempotent and does not duplicate capability IDs or capability kind/key identities. | `passing-template-seed-hardening-tests.txt` | `Passed` |
| Template/policy hardening does not regress SB01-SB06 contracts. | `regression-capability-contracts-through-sb07.txt` | `Passed` |
| Template loader/materializer remain cached and do not introduce per-call template parsing. | `static-performance-scan.txt` | `Passed` |
| No new active hardcoded seed fallback path exists. | `source-assertions.txt` shows active template loader/materializer calls and only inactive legacy helper definitions. | `Passed with SB11 cleanup risk` |

## Diagnostic Shape

- Template diagnostics include category, capability key where applicable, template path, field path, message, and repair hint.
- Access-policy diagnostics include selector field path, invalid or unknown value in the message, effect/scope context in reference validation, and repair hints.
- Required-capability denial uses typed `CapabilityDiagnosticCategory.RequiredCapabilityDenied` and preserves the correlation ID.

## Browser Validation

- `N/A`: SB07 has no browser-visible surface.
