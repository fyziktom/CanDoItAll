# Normalized Requirements

| ID | Requirement | Priority | Proof |
| --- | --- | --- | --- |
| RQ-001 | Keep refactor module-local under `CanDoItAll.Modules.Processes`. | Must | Source scan no Core/no driver API. |
| RQ-002 | Do not create `CanDoItAll.Processes.Core`. | Must | `Test-Path`/`rg` proof. |
| RQ-003 | Preserve dispatch route order exactly. | Must | Unit test and source assertion. |
| RQ-004 | Preserve durable claim acquisition semantics. | Must | Claim store tests and integration smoke. |
| RQ-005 | Preserve claim renewal/heartbeat semantics. | Must | Heartbeat coordinator tests. |
| RQ-006 | Preserve claim release in all success/failure/cancellation paths. | Must | Exception/finally tests and source scan. |
| RQ-007 | Preserve database requirement block behavior. | Must | Focused integration/route tests. |
| RQ-008 | Preserve upstream materialization behavior. | Must | Materialization route tests. |
| RQ-009 | Preserve stranded artifact recovery route behavior. | Must | Recovery route test. |
| RQ-010 | Preserve subprocess route behavior. | Must | Existing subprocess tests plus route order test. |
| RQ-011 | Preserve workflow route behavior. | Must | Workflow route test. |
| RQ-012 | Preserve direct-agent execution/finalization behavior. | Must | Execution route smoke. |
| RQ-013 | Preserve competing execution and run-closed checks. | Must | Focused tests. |
| RQ-014 | Preserve exception/failure transition behavior. | Must | Failure closure tests. |
| RQ-015 | Reduce `Dispatch.cs` materially without deleting functionality. | Should | Line count and source proof. |
| RQ-016 | Do not add UI/small/medium/mobile/browser screenshots. | Must | No UI/viewport scan. |
| RQ-017 | Keep future drivers documentation-only. | Must | No driver API scan. |
| RQ-018 | Record every subbundle individually in execution report. | Must | Execution report gate row scan. |
