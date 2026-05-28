# SB01 Proof Manifest

## Scope

- Subbundle: SB01 `01-01-live-process-approval-actions`
- Semantic invariant contract: bundle://proof/SB01/semantic-invariants.md
- Raw note owned: N001

## Source Proof

- repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor
  - SHA-256: 415183de223b3deb9434a3a9b83d4ca2ae71242aa8297423909406f43befe9f2
- repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessLiveEscalationActionPolicy.cs
  - SHA-256: 085333272b02b9bea6896d4ce4b2884090f253c9cafb185be2a4cada38d19085
- repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationModels.cs
  - SHA-256: 15f1ab84d16ffb8af485f7493c4a210266cf28ea0ab72a38834e6d2ed6b518ff
- repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs
  - SHA-256: 27743f31a1cbee43c4745c9dfc8c5d55b89329fcd91dfbbd0415d8b2c539dd6c
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs
  - SHA-256: eb39b23fa58fe6faaa0237c815b9e30cee2cad23dcb14c426663cde916df4585
- repo://tests/CanDoItAll.Tests.Integration/ProcessLiveEscalationActionPolicyTests.cs
  - SHA-256: 800ef092941bf73ddfbb39f400e4ef2f0c14a3253fc55a576b5d8fb09098534c
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
  - SHA-256: f778092fcb016abc972f16d84f85af3dbfe5ff414536d190d4a5fbd3b6d78997

## Command Proof

- Passing transcript: bundle://proof/SB01/transcripts/focused-test-success.md
- Anti-stub audit transcript: bundle://proof/SB01/transcripts/anti-stub-audit.md
- Failing-first: n/a - process non-production live state already reproduced the defect before this repair; the proof closure uses the live blocked escalation plus regression tests instead of a synthetic failing test run.

## Test Proof

- Test name: ProcessLiveEscalationActionPolicyTests.Blocked_step_escalation_requests_rework_instead_of_approval
- Test name: ProcessLiveEscalationActionPolicyTests.Approval_required_with_source_approval_uses_direct_approval_actions
- Test name: ProcessLiveEscalationActionPolicyTests.Approval_required_without_source_approval_does_not_fake_a_decision
- Test name: ProcessRunAutomationDispatchServiceTests.BuildProcessInvocationMetadataJson_grants_read_only_upstream_external_artifact_paths_for_managed_review_contract

## Semantic Proof

- Expected behavior: blocked-step escalations use rework/resolve actions; true approvals use direct continuation only with source ids.
- Disallowed shallow implementation: label-only changes and manager-chat continuation for blocked-step recovery are rejected by source design and focused tests.
- Semantic positive proof: bundle://proof/SB01/transcripts/focused-test-success.md plus live run `01ee78c6-077e-4a6c-8139-1f4120e659a5` completed after corrected rework.
- Adversarial negative proof: `Approval_required_without_source_approval_does_not_fake_a_decision` proves approval buttons are not rendered without source approval metadata.
- Anti-stub audit: no placeholder implementations were found; see bundle://proof/SB01/transcripts/anti-stub-audit.md.

