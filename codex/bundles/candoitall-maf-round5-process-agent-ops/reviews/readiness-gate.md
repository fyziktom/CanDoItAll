# Readiness Gate

Run in the repository root after implementation.

## Stop-the-line checks

```bash
# Must return no provider-key-looking values. The implementation should also include a committed test for this.
git grep -nE 'sk-(proj-)?[A-Za-z0-9_-]{20,}' -- . ':!codex/bundles/**' ':!**/bin/**' ':!**/obj/**'

# Current execution report must exist.
test -f 01-execution-report.md
```

## Build and default tests

```bash
dotnet --info
dotnet restore CanDoItAll.slnx
dotnet build CanDoItAll.slnx --configuration Release --no-restore /m:1
dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "Category!=Quarantined&Category!=LiveProcess&Category!=PlaywrightEvidence"
```

## Focused gates

```bash
dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "SecretScanning|SnapshotIntegrity|AgentStructuredOutput|Finalizer|AgentToolPolicy|ProcessToolPolicy"
dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "AgentRecoveryDecision|AgentReworkPacket|ProofFingerprint|RetryLedger|ProcessEscalation"
dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "ProcessWorkspace|ApprovalConsole|ReworkConsole|EscalationQueue|AttemptTimeline"
```

## Optional/live gates

```bash
dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "Category=LiveProcess"
dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "Category=StablePlaywright"
dotnet test CanDoItAll.slnx --configuration Release --no-build --filter "Category=DotNetWatchStable"
```

## Report requirements

The final report must include exact files changed, tests added/updated, every command run with exit code, quarantined tests and why, remaining failures, confirmation that no tracked provider key pattern remains, and confirmation that no raw secret value is printed.
