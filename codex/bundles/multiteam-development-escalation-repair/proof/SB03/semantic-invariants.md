# SB03 Semantic Invariants

## Invariant MTE-SB03-READINESS

- Invariant ID: `MTE-SB03-READINESS`
- Source raw note: HR matching should prevent missing-tool and missing-allowance trouble before process execution escalates.
- Expected behavior: Launch validation flags semantically impossible operation contracts, finalizer repair preserves prior tool/artifact evidence, transient provider/runtime failures can retry safely, and real rights/tool blockers remain blockers.
- Disallowed shallow implementation: Silently retrying every blocked result, hiding real rights failures, or treating all provider errors as success.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first.txt` records the missing semantic readiness and self-evidence retry gaps.
- Passing test: `bundle://proof/SB03/transcripts/passing.txt` records focused adapter, resolver, and finalizer tests passing.
- Changed source files: `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeIntegrationServices.cs` with hash `30B6C7C742D51DB17CC71F8C85FB9E45791491AE7B54721D1B9FC42947BAA677`; `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` with hash `C3FA37C7E9E3031B547704AD8492E7264481B8450ECB060FA5D3A741433BEA23`.
- Production assertions: Adapter results now distinguish managed-artifact write retries, transient execution retries, and true rights/tool blockers.
- Red-team negative case: A rights-boundary blocker must not be reclassified as retryable.
- Downstream dependency check: SB04 uses these guardrails to complete the Calculator proof run without repeating the previous false escalation.
