# SB05 Semantic Invariants

## Invariant SB05_INV_001

- Invariant ID: `SB05_INV_001`
- Source raw note: "Introduce the process-owned execution client/facade with no behavior change and initial tests."
- Expected behavior: Processes owns an internal facade for automation execution operations, and each facade method delegates to the existing AgentFramework workspace service without alternate runtime paths.
- Disallowed shallow implementation: Adding only an interface or DI registration while leaving delegation untested, silently swallowing null request/model inputs, or moving dispatcher calls before SB06.
- Failing-first test: `bundle://proof/SB05/transcripts/process-automation-execution-client-tests.failing-first.txt` records the first targeted run with `SB05_INV_001 SB05_INV_002 SB05_INV_003 SB05_INV_004`; it failed before the fake workspace proxy could execute the delegation tests.
- Passing test: `bundle://proof/SB05/transcripts/process-automation-execution-client-tests.txt`; test name `ProcessAutomationExecutionClientTests`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionClient.cs`; `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`; `repo://tests/CanDoItAll.Tests.Integration/ProcessAutomationExecutionClientTests.cs`; hashes are recorded in `bundle://proof/SB05/transcripts/hashes.txt`.
- Production assertions: `bundle://proof/SB05/source-assertions/facade-foundation.md`.
- Red-team negative case: Removing a facade method, bypassing the fake workspace service, changing DI lifetime, or migrating dispatcher direct calls early would fail the targeted tests or the SB05 source scans.
- Downstream dependency check: SB06 may now migrate dispatcher calls to `IProcessAutomationExecutionClient`; SB05 confirms those dispatcher direct calls still exist as the SB06 source target.

## Invariant SB05_INV_004

- Invariant ID: `SB05_INV_004`
- Source raw note: "Do not run small, medium, or mobile UI validation."
- Expected behavior: SB05 remains a service/test change with browser validation recorded as N/A and no screenshot artifacts.
- Disallowed shallow implementation: Producing unrelated viewport proof for a non-UI service change.
- Failing-first test: N/A - the viewport policy is unchanged by this service foundation.
- Passing test: `bundle://proof/SB05/transcripts/facade-source-registration-scan.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionClient.cs`; `repo://tests/CanDoItAll.Tests.Integration/ProcessAutomationExecutionClientTests.cs`.
- Production assertions: `bundle://proof/SB05/source-assertions/facade-foundation.md`.
- Red-team negative case: Any mobile, small-screen, or medium-screen proof artifact would reopen SB05 under the bundle policy.
- Downstream dependency check: SB06 inherits browser N/A unless it touches rendered UI, which is not expected.
