# Codex Report vs Actual Snapshot

The pasted report claims an execution report, secret scanning, typed recovery models, rework packets, proof fingerprints, process tool governance, and focused tests.

The uploaded ZIP did not contain the expected current `01-execution-report.md` at repository root or an equivalent current report path.

The following claimed artifacts were not found in the source/test/doc areas inspected:

- `SecretScanningTests.cs`
- `AgentRecoveryModels.cs`
- `AgentRecoveryModelsTests.cs`
- `AgentReworkPacket`
- `ProofFingerprint`
- `RecoveryLedger`

A real-looking OpenAI API key pattern still exists in `src/CanDoItAll.Web/appsettings.json:33`.

## Required guardrail

Codex must add a snapshot integrity validation step that compares the execution report to repository reality:

- every claimed file path must exist,
- every claimed test class must exist,
- every claimed command must include command, working directory, exit code, and summary,
- secret scans must run against tracked files and redact matches,
- the report must fail the bundle if any claimed deliverable is missing.
