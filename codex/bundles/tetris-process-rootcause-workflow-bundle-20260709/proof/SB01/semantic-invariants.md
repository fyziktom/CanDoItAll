# SB01 Semantic Invariants

- Invariant ID: `SB01-INV-extracted-gate`
- Source raw note: GPTPro RC8 and the architecture requirement to extract completion gate evaluation from the adapter partial cluster.
- Expected behavior: Completion gate issues are aggregated and ordered by a dedicated evaluator that accepts a narrow context and delegates gate-specific checks.
- Disallowed shallow implementation: A renamed adapter helper that still requires full MAF runtime construction to test ordering or deduplication.
- Failing-first test: `bundle://proof/shared/transcripts/failing-first.txt`
- Passing test: `ProcessCompletionGateEvaluator_orders_and_deduplicates_issues_without_adapter_runtime` in `bundle://proof/shared/transcripts/passing-tests.txt`
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionGateEvaluator.cs`
- Production assertions: Adapter completion paths now call the evaluator with `ProcessCompletionGateContext`.
- Red-team negative case: Duplicate completion issues with the same key are suppressed instead of double-counted.
- Downstream dependency check: SB03 and SB04 build on the evaluator rather than adding another adapter-local ordering path.
