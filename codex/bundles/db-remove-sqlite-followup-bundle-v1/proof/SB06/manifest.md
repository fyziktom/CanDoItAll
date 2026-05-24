# Proof manifest SB06

## Status

Complete.

## Commands

- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "Category!=Quarantined" -v:minimal`
- Targeted runtime slices were also run during implementation for process outbox, runtime hosted worker policy, automation runtime, and scheduler/planner concurrency.

## Evidence files

- `evidence/SB06/dotnet-test-integration-nonquarantined.log`
- `evidence/SB08/dotnet-test-integration-nonquarantined.log`

## Notes

The durable claim audit found existing process/automation/scheduler paths already use EF atomic update, lock token, lease, and idempotency patterns rather than unsafe read-then-update dispatch. Outbox worker concurrency now defaults to `2`, is clamped by typed constants, and remains configurable. Added negative concurrency tests for process outbox and scheduler/planner dedupe behavior.
