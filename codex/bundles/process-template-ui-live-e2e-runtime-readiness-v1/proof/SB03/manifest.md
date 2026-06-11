# SB03 Proof Manifest

## Scope
- Subbundle: `SB03`
- Invariant contract: `bundle://proof/SB03/semantic-invariants.md`
- Positive test name: `Blazor_app_delivery_template_SB03_INV_001_completes_through_automation_dispatch_finalizer_and_readback`
- Negative test name: `Blazor_app_delivery_template_SB03_INV_002_missing_process_mock_role_mapping_fails_before_dispatch`
- Source files:
  - `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs`
  - `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs`

## Changed-File Hashes
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs` SHA-256: `D263FDEE95AA5D0EA47CFD6C6E81325681B53853C377A75981B11FFE7FBA168D`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs` SHA-256: `7594BBCB64D091A8F23A1CE4A8C776DFDE8EA06D5DB16E7AEEDB9A827BC6AAB3`

## Source Proof
- Strengthened the Blazor automation proof in `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs` with explicit outbox completion, execution-run mapping, process-mock source/provider/model, run id, completed/succeeded execution state, branch outcome, finalizer summary, artifact persistence, and managed-file readback assertions.
- Added `SB03_INV_002`, a missing process-mock role mapping negative test that omits `blazor-engineer`, asserts the support guard message, and verifies no project run or `dispatch-run-automation` outbox record was created.
- Renamed the older manual-transition Blazor test to `Blazor_app_delivery_template_manual_contract_test_runs_from_project_structure_context_with_artifacts_and_readback` so it is not mistaken for automation-path proof.
- Source assertion transcript: `bundle://proof/SB03/transcripts/source-assertions.txt`
- Suppression scan: `bundle://proof/SB03/transcripts/suppress-dispatch-scan.txt`
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`

## Test Proof
- Passing focused integration transcript: `bundle://proof/SB03/transcripts/focused-integration.txt`
- Command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Blazor_app_delivery_template_SB03_INV"`
- Result: focused SB03 integration tests passed with exit code 0; 2 tests passed.
- Code-first guard transcript: `bundle://proof/SB03/transcripts/code-first-guard.txt`

## Adversarial Negative Proof
- Failing-first transcript: `bundle://proof/SB03/transcripts/failing-first-source-assertion.txt`
- The failing-first source assertion exits non-zero against `HEAD` because the baseline did not contain `SB03_INV_002` missing-role validation.

## Browser Evidence
- N/A. SB03 does not change a browser-visible route; SB02 owns the browser launch/readback path and SB06 may add run-detail browser proof for runtime-host readback.

## Semantic Adequacy
- Raw note owned: Determine whether the Blazor/.NET representative process works like before through actual automation dispatch.
- Shipped behavior: `SB03_INV_001` proves active-path Blazor process-mock dispatch, finalizer summaries, outbox completion, execution-run mapping, branch selection, artifact records, and managed-file readback. `SB03_INV_002` proves missing required role mapping fails before dispatch without creating a run or automation outbox record.
- Shallow-pass trap: A manual contract test using `SuppressAutomationDispatch = true` can prove service transitions, but it cannot prove process-mock automation dispatch, outbox processing, execution-run finalizer behavior, or managed artifact output.

