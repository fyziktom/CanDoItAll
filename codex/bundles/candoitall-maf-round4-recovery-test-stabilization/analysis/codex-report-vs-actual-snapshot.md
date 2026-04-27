# Codex Report vs Actual Snapshot

## Why this matters

The latest Codex report claims several round 3 changes were implemented. The attached ZIP does not contain multiple claimed artifacts. This is a process risk: future reports must be backed by repository evidence, not just narrative claims.

## Claimed by Codex but not found

| Codex claim | Snapshot evidence | Required action |
|---|---|---|
| Removed committed provider key material | `src/CanDoItAll.Web/appsettings.json:33` still contains a real-looking provider key | Remove, rotate/revoke externally, add secret scan |
| Added secret scanning | `SecretScanningTests.cs` not found | Add test and/or script, run it in CI/default verification |
| Added typed recovery decisions | `AgentRecoveryModels.cs` not found; no `AgentRecoveryDecision` found | Implement models and tests |
| Added rework packets | no `AgentReworkPacket` found | Implement packet model, persistence, prompt serialization, tests |
| Added proof fingerprints | no `ProofFingerprint` found | Implement fingerprinting, invalidation, and reuse tests |
| Added recovery ledger/backoff | no `RecoveryLedger` found | Implement durable per-step attempt ledger |
| Classified process mutation tools as mutations | `IsMutationTool(...)` lists workspace tools only | Add process tool metadata catalog and tests |
| Approval-wrapped exposed process mutation tools | process tools are added directly to `composition.State.Tools` | Implement explicit approval/policy wrapping or document and test governed auto-approval policy |

## Required anti-regression rule

Add a repository-level verification test or script that checks for the presence of required round-deliverable files/classes before the bundle is marked complete. This is not a substitute for behavior tests; it is a guard against reporting artifacts that were never committed.

## Execution report format

Codex must provide an execution report with:

- exact files changed;
- exact new files created;
- exact tests added;
- exact commands run;
- pass/fail command outcomes;
- any quarantined tests and why;
- any claims backed by file paths and test names.

Claims such as "added secret scanning" must include the test/script path and a passing command that executed it.
