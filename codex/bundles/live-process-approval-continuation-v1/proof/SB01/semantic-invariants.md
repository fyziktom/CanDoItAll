# SB01 Semantic Invariants

## Invariant

- Invariant ID: SB01-LIVE-ACTION-SEMANTICS
- Source raw note: N001 reported that clicking `Approve` in Live Processes on port 5032 did not continue a blocked process.
- Expected behavior: blocked-step escalations render and execute governed rework or resolve actions; true approval escalations require source execution and approval ids before direct continuation.
- Disallowed shallow implementation: renaming a button while still sending blocked-step continuation through manager chat, or allowing approval to silently override a non-approval blocked-step contract.
- Failing-first test: the live run had `BlockedStep` escalation `03fa3262-a7ec-401b-a0e9-2a97533a7508`; prior quick action did not continue the run and lacked required external-target receipts.
- Passing test: bundle://proof/SB01/transcripts/focused-test-success.md records 4 passing focused tests.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor; repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessLiveEscalationActionPolicy.cs; repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExecutionMetadata.cs.
- Production assertions: live process run `01ee78c6-077e-4a6c-8139-1f4120e659a5` completed after rework packet `8bb0da31-0215-461e-942a-201df38ff3d6`; execution run `2635c7a1-f057-418e-b929-32b21c241ba7` recorded successful stat and read receipts for both grounded external product files.
- Red-team negative case: `ProcessLiveEscalationActionPolicyTests.Approval_required_without_source_approval_does_not_fake_a_decision` prevents a synthetic approval action when source approval metadata is missing.
- Downstream dependency check: Live Processes HTML/API proof showed `Request rework` and no blocked-step `Approve`; process health ended with zero open escalations, zero pending outbox records, and zero missing artifacts.

