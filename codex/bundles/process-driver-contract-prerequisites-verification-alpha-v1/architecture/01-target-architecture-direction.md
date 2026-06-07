# Target Architecture Direction

## Stable Core

`CanDoItAll.Processes.Core` should contain only:
- immutable read models,
- deterministic rules,
- descriptors,
- diagnostic facts,
- compatibility-safe value objects.

Core must not contain:
- EF or database access,
- filesystem/workspace/storage,
- AgentFramework execution,
- claim/lease/heartbeat,
- transition execution,
- finalizer application,
- process mutation,
- driver runtime implementation.

## Driver Direction

Drivers eventually sit outside Core and consume:
- Core descriptors,
- process-module read models,
- existing proof artifacts,
- permission/audit policy,
- sandbox/command policy.

The first driver should be verification-only and should inspect existing evidence only.
