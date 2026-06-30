# SB08 Semantic Invariants

| Invariant | Proof | Status |
| --- | --- | --- |
| `SB08_INV_MAF_ACCESS_001` process allowed operations compile to shared capability policies and record effective-set diagnostics for denied workspace tools. | `passing-maf-access-tests.txt` | `Passed` |
| `SB08_INV_MAF_ACCESS_002` runtime-provider tools are filtered through the shared policy evaluator and denied provider tools are recorded as access diagnostics. | `passing-maf-access-tests.txt` | `Passed` |
| `SB08_INV_MAF_ACCESS_003` catalog Skill and Tool descriptors used by MAF access evaluation come from isolated descriptor/exposure factories and preserve template source paths. | `failing-first-descriptor-factory-test.txt`, `passing-descriptor-factory-test.txt` | `Passed` |
| MAF runtime state exposes a typed `EffectiveCapabilitySet` containing allowed descriptors and suppression diagnostics. | `passing-maf-access-tests.txt`, `source-assertions.txt` | `Passed` |
| Configured workspace/storage tools and registered runtime-provider tools are evaluated by the same runtime access plan as catalog capabilities. | `regression-maf-tool-provider-composition-tests.txt`, `source-assertions.txt` | `Passed` |
| Old active process-scope suppression methods are not present as runtime fallback paths. | `source-assertions.txt` | `Passed` |
| Existing execution capability filtering behavior is preserved. | `regression-capability-filtering-integration-tests.txt` | `Passed` |
| Template seed and hardening behavior remains stable after moving process policy compilation to Core. | `regression-template-seed-tests.txt` | `Passed` |
| The runtime migration does not introduce blocking waits, sleeps, artificial delays, or implementation stubs. | `static-performance-scan.txt`, `anti-stub-audit.txt` | `Passed` |

## Diagnostic Shape

- Suppressed capability diagnostics include identity, optional rule id, scope, selector kind, category, reason, repair hint, and correlation id.
- Availability failures use `CapabilityDiagnosticCategory.CapabilityUnavailable`.
- Policy denials use `CapabilityDiagnosticCategory.AccessPolicy` unless the denied item is required, in which case they use `CapabilityDiagnosticCategory.RequiredCapabilityDenied`.
- Runtime-provider denials are appended to the same effective capability diagnostic collection as initial catalog/configured capability denials.

## Browser Validation

- `N/A`: SB08 has no browser-visible surface. Small and medium viewport UI validation was skipped per user instruction because the app targets large screens only.
