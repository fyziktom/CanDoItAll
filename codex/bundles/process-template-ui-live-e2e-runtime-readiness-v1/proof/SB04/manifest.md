# SB04 Proof Manifest

## Scope
- Subbundle: `SB04`
- Invariant contract: `bundle://proof/SB04/semantic-invariants.md`
- Automation test name: `Software_delivery_template_SB04_INV_001_completes_multi_team_governance_through_automation_dispatch`
- Catalog test name: `Process_template_catalog_SB04_INV_002_uses_software_delivery_as_canonical_multi_team_representative_without_alias_key`
- Source files:
  - `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs`
  - `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs`
  - `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs`

## Changed-File Hashes
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs` SHA-256: `1B015FC87D1E252D8BE309E484FC60DE19AC354FD1D7DF06CD05576D3436B722`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs` SHA-256: `99B2143C51ACBBBFF8A45941BF96562CBD24AA24721FBA0B8B19471A637A606E`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateAutomationTestSupport.cs` SHA-256: `7594BBCB64D091A8F23A1CE4A8C776DFDE8EA06D5DB16E7AEEDB9A827BC6AAB3`

## Source Proof
- Strengthened the `software-delivery` E2E in `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs` with direct governance execution-run assertions, project-scoped run readback, seven-role assignment coverage, finalizer summaries, and managed-file readback for release/governance artifacts.
- Added `SB04_INV_002` in `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateGovernanceTests.cs` to document the no-alias decision: `multi-team-development` is not a process key; `software-delivery` is the canonical mapped representative for multi-team development.
- Source assertion transcript: `bundle://proof/SB04/transcripts/source-assertions.txt`
- Process Core leakage scan: `bundle://proof/SB04/transcripts/process-core-leakage-scan.txt`
- Anti-stub audit: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`

## Test Proof
- Passing focused integration transcript: `bundle://proof/SB04/transcripts/focused-integration.txt`
- Command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --configuration Debug --no-restore --filter "FullyQualifiedName~Software_delivery_template_SB04_INV_001|FullyQualifiedName~Process_template_catalog_SB04_INV_002"`
- Result: focused SB04 integration tests passed with exit code 0; 2 tests passed.
- Code-first guard transcript: `bundle://proof/SB04/transcripts/code-first-guard.txt`

## Adversarial Negative Proof
- Failing-first transcript: `bundle://proof/SB04/transcripts/failing-first-source-assertion.txt`
- The failing-first source assertion exits non-zero against `HEAD` because the baseline did not contain the SB04 catalog no-alias test, role assignment assertions, direct governance execution-run assertions, or post-release learning managed artifact assertion.

## Browser Evidence
- N/A. SB04 does not change UI/catalog browser wording; SB02 owns the browser launch/readback route.

## Semantic Adequacy
- Raw note owned: Determine whether multi-team development works as a representative process.
- Shipped behavior: `SB04_INV_001` proves the canonical `software-delivery` representative through production automation dispatch, multi-role governance assignments, peer review, QA, security, release approval, rollout, post-release learning, project-structure/writeback artifacts, and managed output readback. `SB04_INV_002` proves the multi-team representative is intentionally `software-delivery`, not a duplicate alias template.
- Shallow-pass trap: A step-status-only run or a duplicate alias key could appear to satisfy "multi-team development" without proving release governance, role coverage, managed writeback artifacts, or unambiguous catalog readback.

