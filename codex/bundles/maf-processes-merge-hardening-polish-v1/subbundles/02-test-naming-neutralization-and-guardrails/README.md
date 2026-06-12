# Test naming neutralization and guardrails

## Status

- `Ready`

## Objective

Remove temporary bundle/subbundle/SB/INV naming from active tests and add durable guardrails so Codex cannot accidentally turn bundle execution IDs into permanent test/API names again.

## Success Criteria

- No active test method name contains `SB###`, `INV###`, `bundle`, `subbundle`, or historical bundle slug terms.
- `ProcessDriverVerificationGatewayTests.cs` method names are semantic and behavior-oriented.
- A guard test or tracked-file scan detects future naming leaks in active tests.
- Bundle-skill tooling remains exempt where the word `bundle` is intrinsic to the tool.

## Covered Inputs

- User specifically called out bundle naming leaks in tests.
- Observed `ProcessDriverVerificationGatewayTests.cs` methods with names such as `Process_driver_verification_gateway_SB018_INV_001_explicitly_runs_all_approved_readonly_lanes`.

## Prerequisites

- SB01 completed, so deleted bundle artifacts do not pollute scans.

## Exact Source References

- `tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationGatewayTests.cs`
- `tests/CanDoItAll.Tests.Unit/ProcessDriverVerificationTestHarness.cs`
- `tests/CanDoItAll.Tests.Unit/AgentRuntimeHardeningStaticRegressionTests.cs`
- Any test discovered by:

```bash
rg -n 'SB[0-9]{2,3}|INV_[0-9]{3}|subbundle|bundle|maf-processes-|process-driver-domain-gateway|process-runtime-live-openai' tests
```

## Deliverables

- Rename SB/INV-labeled tests to semantic names. Suggested replacements for observed names:

| Current pattern | Suggested semantic name |
| --- | --- |
| `Process_driver_verification_gateway_SB018_INV_001_explicitly_runs_all_approved_readonly_lanes` | `Process_driver_verification_gateway_runs_all_approved_readonly_lanes` |
| `Process_driver_verification_gateway_SB018_INV_002_source_has_no_dynamic_registry_selector_di_or_manager_surface` | `Process_driver_verification_gateway_source_has_no_dynamic_registry_selector_di_or_manager_surface` |
| `Process_driver_verification_gateway_SB012_INV_001_runs_explicit_typed_batch_without_generic_dispatch` | `Process_driver_verification_gateway_runs_explicit_typed_batch_without_generic_dispatch` |
| `Process_driver_verification_gateway_SB018_INV_003_rejects_side_effects_across_all_domain_lanes` | `Process_driver_verification_gateway_rejects_side_effects_across_all_readonly_lanes` |
| `Process_driver_verification_gateway_SB033_INV_001_audit_redaction_and_no_mutation_cover_accepted_and_denied_responses` | `Process_driver_verification_gateway_audit_redaction_and_no_mutation_cover_accepted_and_denied_responses` |
| `Process_driver_verification_gateway_SB024_INV_001_closes_no_secret_no_mutation_and_hash_mismatch_gates_across_all_lanes` | `Process_driver_verification_gateway_closes_secret_mutation_and_hash_mismatch_gates_across_all_lanes` |

- Add a unit guard, e.g. `RepositoryNamingHygieneTests.Active_tests_do_not_contain_work_package_identifiers`, that scans tracked active files and fails on:
  - `SB\d{2,3}` in test names,
  - `INV_\d{3}` in test names,
  - historical bundle slugs such as `maf-processes-provider-hardening-followup-v1`, `process-runtime-live-openai-verification-host-alpha-v1`, `process-driver-domain-gateway-adapters-stabilization-v1`,
  - `subbundle` in tests outside bundle-preparation tooling,
  - `bundle` in test method names unless the test is explicitly testing the bundle-preparation skill under `codex/skills/bundles`.
- Prefer a test that extracts method names from `.cs` files instead of banning all English words in arbitrary comments.

## Dependency Impact

- SB04 should include this naming guard in final boundary tests.
- SB05 final scans rely on this subbundle to prove temporary plan terminology is not permanent.

## Validation Depth

- Critical test hygiene foundation.

## Implementation Steps

1. Run the discovery scan:

```bash
rg -n 'SB[0-9]{2,3}|INV_[0-9]{3}|subbundle|bundle|maf-processes-|process-driver-domain-gateway|process-runtime-live-openai' tests src Templates docs README.md --glob '!codex/skills/**'
```

2. Rename only active test methods and test display names. Do not weaken assertions.
3. Remove or rewrite active test comments that reference subbundle IDs as permanent proof IDs. Use behavior language instead.
4. Add a tracked-file scanner helper if SB01 did not already create one; reuse it if present.
5. Add naming guard test focused on active tests and active source/docs/templates. Exclude:
   - `codex/skills/**`,
   - generated `bin/obj`,
   - local transient `.codex*`, `.artifacts`, `.playwright-mcp`.
6. Ensure the guard failure message lists file path, line number, and matched forbidden term.
7. Run focused test suite.

## Scope Exceptions

- Do not rename production classes only because they contain the word `ProcessDriver`; that is domain vocabulary.
- Do not ban `bundle` inside `codex/skills/bundles/**`.
- Do not rewrite old Git commit messages.

## Do Not Do

- Do not delete assertions because their names contain SB markers; rename them.
- Do not replace source scans with report-only notes.
- Do not use string concatenation tricks in tests to hide forbidden sample names unless the purpose is specifically testing a scanner sample. Prefer clean semantic names.

## Acceptance Checklist

- [ ] `ProcessDriverVerificationGatewayTests.cs` contains semantic test names only.
- [ ] Discovery scan returns no active leaks outside allowed paths.
- [ ] New/updated naming guard test passes.
- [ ] Existing process/driver tests still pass.

## Proof Required

- Before/after naming scan output.
- `dotnet test tests/CanDoItAll.Tests.Unit --filter "ProcessDriverVerificationGatewayTests|RepositoryNamingHygiene"`
- `dotnet test tests/CanDoItAll.Tests.Unit --filter AgentRuntimeHardeningStaticRegression`

## Browser Validation Logging

- N/A

## Progression Gate

SB03 may start only after active test names and active source/test/docs scans are free of work-package identifiers.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Remove SB/INV/bundle/subbundle naming leaks from active tests and add a future naming guard. Preserve all assertions and behavior. Exclude codex/skills/bundles because it is legitimate bundle tooling. Capture before/after scans and focused test output.
```
