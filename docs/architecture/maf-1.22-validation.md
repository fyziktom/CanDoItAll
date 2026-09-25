# MAF 1.22 CI handoff

Checkpoint: 2026-09-25. Branch: `maf-update-and-hil`. Starting HEAD:
`d67ecc6f4550c24ba316ea895812d4b65d0ae868`.

The candidate is ready for a GitHub CI run. The operator requested focused local tests
and deferred the next full suite to CI. This changes the execution schedule in the
original preparation bundle; it does not assert completion of its browser or live-provider
acceptance criteria. The preparation package remains unchanged.

## Supplied CI failure

The 2026-09-23 logs in `logs_97185037017.zip` show the same two unit failures on Windows,
Linux and macOS. All three servers reported PostgreSQL `180006`; all 3,131 integration
tests passed on each platform. Container and static jobs reported no errors.

| Regression | Repair already present in the candidate |
| --- | --- |
| `MafWorkflowExecutorFailureDiagnosticsTests.Executor_failure_surfaces_root_cause_in_summary_events_and_diagnostic_payload` | `WorkflowBackendProgressEventObserver` includes the failure detail after the payload policy has applied redaction and size limits. |
| `WorkflowFoundationTests.RuntimeManager_keeps_lifecycle_events_safe_and_persists_payload_artifacts` | The test requires bounded inline output, truncation and a reference to the persisted artifact for a payload exceeding its 160-character limit. Secret-redaction assertions remain. |

No PostgreSQL configuration, CI timeout, exclusion or application deadline was changed
for this diagnosis. The dedicated PostgreSQL migration/restart and restore CI steps still
need the next run because the failed stable step prevented them from executing.

## Source and recovered evidence

All 109 files in the last `working-source-9-ui-1` manifest matched the working tree
byte-for-byte before the documentation updates in this checkpoint. The one recorded
deletion is still absent. Its backend inputs match `working-source-9`; the subsequent
change only corrects provider-kind/model interaction order in the Playwright helper.

The original TRX files confirm 15,256 passed, zero failed and zero skipped on each of
Windows and actual Linux. Fifty of the 51 receipt artifacts retain their recorded hashes.
The remaining Windows command log retains the exact hashed 7,872-byte prefix; later
headless/browser commands were appended. The old receipt was not rewritten. Earlier
failed attempts remain historical evidence, not passing results.

Both recorded full builds used Components `f258ab6a959a97fa16c01d0858e7dc122728a11a`
and FileTools `498b36825bd5a5222429972af120b04becf4b3f6`. Fresh focused execution used
the existing isolated checkout with those dependencies and verified its 109 source files.
The main-checkout workflow-adapter build passed with zero warnings/errors. A subsequent
main-checkout test build hit DLL locks held by the running user application and was stopped;
the isolated test builds completed successfully without stopping that application.

The prior actual-diff CodeAnalytics analysis loaded 149 healthy projects and 10,769 source
test methods but returned incomplete/low-confidence `AllSuppliedSuites` impact because of
unresolved dispatch. This is not evidence that the focused tests exhaust the impact.
A fresh workflow-adapter snapshot (`snap-20260925093943-6cddd147`) loaded 56 types and
372 members with no diagnostics. The cross-cutting package/contract changes remain the
reason for the upcoming broad CI gate.

## Fresh local verification

SDK: `10.0.303`; configuration: `Release`; VSTest/xUnit. Every new filter was discovered
before execution against refreshed test binaries. Expected and actual discovery agreed.

| Selection | Expected / discovered | Passed / failed / skipped |
| --- | --- | --- |
| Exact two CI regressions above | 2 / 2 | 2 / 0 / 0 |
| MAF and process unit classes below | 448 / 448 | 448 / 0 / 0 |
| `AgentPendingApprovalCancellationUiTests`, `ProcessRunCancellationActionTests` | 20 / 20 | 20 / 0 / 0 |

The 448-case selection contains `Maf122UpgradeCompatibilityTests`,
`Maf122DynamicToolIsolationTests`, `Maf122RetainedNativeStateTests`,
`Maf122RetainedStatePolicyTests`, `MafApprovalCacheAuthorityTests`,
`A2ARemoteAgentToolLifetimeTests`, `AgentPendingApprovalCancellationTests`,
`ProcessProviderFailureRecoveryTests`, `MafWorkflowHumanInLoopTests`,
`ProcessRuntimeIntegrationAdapterTests`, `AgentFinalizerPolicyTests` and
`WorkflowProgressEventIdentityTests`. Its execution took 11 seconds; the CI regressions
and component selection each took less than one second, excluding builds/discovery.

Reproduce a selection using an explicit OR filter, with a trailing dot after each class
name, or use exact fully qualified method names for the two CI regressions:

```powershell
$project = "tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj"
$filter = "FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.MafWorkflowExecutorFailureDiagnosticsTests.Executor_failure_surfaces_root_cause_in_summary_events_and_diagnostic_payload|FullyQualifiedName=CanDoItAll.Tests.Unit.AgentFramework.WorkflowFoundationTests.RuntimeManager_keeps_lifecycle_events_safe_and_persists_payload_artifacts"
dotnet test $project --configuration Release --list-tests --filter $filter /m:1
# Require exactly two discovered cases before execution.
dotnet test $project --configuration Release --no-build --no-restore --filter $filter /m:1
```

The complete portability scan includes untracked proposed files and passes final
enforcement without `--write-baseline`: 14,958 reviewed executable-source findings
unchanged. Both tooling test programs pass (six baseline tests and four secret-scanner
tests). No baseline refresh was needed in this recovery checkpoint. Validation follows
the repository Testing guide and `CanDoItAll.SharedInfo/docs/standards/tooling.md`;
no shared templates or sibling repositories were changed. Documentation validation
passed for 235 maintained Markdown files, preparation-package integrity passed and
`git diff --check` passed.

Local logs, TRX files, exact unit/component filter strings, source checks and receipt
recovery details are retained under `output/maf-1.22/ci-readiness-20260925/`. They are
ignored execution artifacts; this document is the maintained handoff.

## Remaining acceptance

- Run the existing GitHub CI workflow for the committed candidate, including all three
  stable platforms, PostgreSQL-specific gates, portability and containers. Components
  continues to resolve from the matching target branch; FileTools retains its pin.
- Full Playwright suites, the controlled browser acceptance harness and remaining
  UI/live-provider journeys have not passed. Playwright compilation/discovery and the
  20 component cases above do not replace browser execution. Earlier provider-creation
  browser attempts failed; the helper correction still needs its browser proof.
- Retained-state deployment/reconciliation and rollback restrictions remain as described
  in [the upgrade notes](maf-1.22-upgrade.md). No deployment is part of this checkpoint.

No full suite was rerun locally in this recovery checkpoint. No commit, push, PR or
deployment was performed. Existing sibling-repository work remains outside this change.

## Commit preparation

The subsequent commit request exposed a stale Git index lock and Git's default line-ending
conversion of sealed evidence. The unowned lock was removed. Scoped `.gitattributes`
entries now preserve the original preparation package and MAF 1.20 fixture bytes, recognize
their CRLF endings and allow the package's intentional Markdown hard breaks. All 31 fixture
hashes and the preparation-package validator pass against the staged Git content itself.
One surplus blank line at the end of an integration test was removed; no test behavior
changed. The staged whitespace check passes.
