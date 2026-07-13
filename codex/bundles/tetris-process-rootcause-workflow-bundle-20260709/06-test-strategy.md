# Test strategy

## Unit tests: completion rules

1. `ReceiptRuleParser_parses_legacy_string_array`
2. `ReceiptRuleParser_parses_branch_aware_object_array`
3. `ReceiptRuleParser_preserves_by_step_object_rules`
4. `ProcessRequiredRuntimeToolNames_extracts_tool_names_from_object_rules`
5. `ProcessLaunchApplicationService_preserves_structured_receipt_rules_after_step_resolution`

## Unit tests: branch-aware receipt gates

1. `QaValidation_quality_accepted_requires_browser_runtime_receipts`
2. `QaValidation_repair_required_skips_acceptance_only_browser_receipts_when_defect_evidence_exists`
3. `QaRecheck_quality_accepted_requires_browser_runtime_receipts`
4. `QaRecheck_repair_escalation_skips_acceptance_only_browser_receipts_when_defect_evidence_exists`
5. `Duplicate_product_and_capability_receipts_are_deduplicated`

## Unit tests: branch-routable issues

1. `QualityAccepted_scaffold_content_failure_routes_to_repair_required`
2. `QaRecheck_quality_accepted_scaffold_failure_routes_to_repair_escalation`
3. `Branch_routable_content_failure_does_not_consume_safe_retry_budget`
4. `Content_failure_without_configured_route_remains_completion_gate_failure`
5. `Repair_required_without_defect_evidence_and_without_browser_proof_is_same_step_retry_or_blocked`

## Unit tests: recovery advice boundaries

1. `GenericRecoveryInstructionBuilder_has_no_dotnet_or_qa_branch_constants`
2. `DotNetSoftwareDeliveryRecoveryAdviceProvider_adds_dotnet_tool_guidance`
3. `RecoveryAdvice_uses_applicable_receipt_rules_for_selected_branch`
4. `RecoveryAdvice_missing_qa_proof_is_not_product_repair_branch`

## Integration tests without LLM

### Incident fixture

Create a synthetic process run fixture for root run `c4888...`:

1. QA attempt 1: `repair-required`, validation receipts only, no concrete product defect.
   - Expected: current-step retry, not branch route.
2. QA attempt 3: `quality-accepted`, full runtime/browser receipts, scaffold content present.
   - Expected: runtime routes `repair-required`, no manager.
3. QA attempt 4: `repair-required`, deterministic scaffold defect evidence, no browser receipts.
   - Expected: branch signal `repair-required`, no manager.

### Repair path fixture

1. Start with scaffold content present.
2. `qa-validation` routes `repair-required`.
3. `quality-repair` removes scaffold and writes repair evidence.
4. `qa-recheck` runs validation/browser proof and accepts.
5. Downstream `release-approval-after-repair` unblocks.

### Acceptance matrix fixture

Create two project structures:

- Calculator-like: simple arithmetic criteria.
- Tetris-like: game loop, keyboard controls, next piece UI, local score persistence.

Assertions:

- Simple calculator can pass with simple proof.
- Tetris-like shell implementation must fail acceptance matrix and route repair.
- No Tetris-specific string appears in generic runtime.

## Architectural tests

Add a test that scans generic runtime/application folders for forbidden domain tokens, with allow-list exceptions.

Suggested forbidden tokens in generic process core/application:

- `Tetris`,
- `Blazor`,
- `Counter.razor`,
- `Weather.razor`,
- `workspace_dotnet_`,
- `qa-validation`,
- `quality-accepted`,
- `repair-required`,
- `repair-escalation`.

Allowed locations:

- `Templates/Processes/processes/software-delivery/**`,
- `src/Modules/CanDoItAll.Modules.Workbench/**`,
- domain-specific recovery providers,
- tests that explicitly test software-delivery behavior.

## Manual regression scenario

After tests pass, run three real process cases:

1. Calculator: should still pass.
2. Tetris with intentionally incomplete first implementation: should route repair and continue, not escalate.
3. Tetris after repair: should pass only after scaffold and acceptance matrix defects are resolved.
