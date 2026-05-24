# SB07 semantic invariants

## Invariant protected

The runtime has exactly one canonical database source of truth. Profile-specific database access is reserved for explicit maintenance, bootstrap, transfer, and admin flows.

## Producer/consumer lifecycle

The canonical runtime profile is produced at service-provider startup. Profile activation produces pending restart state for the next process, not a live context drain.

## Positive proof

Architecture, runtime, and component tests all pass for the intended canonical/pending-restart behavior.

## Adversarial negative proof

The architecture test scans production source for unapproved `IProfileAppDbContextFactory` usage.

## Anti-stub proof

The test walks real `src/**/*.cs` files and reports exact offending paths.
