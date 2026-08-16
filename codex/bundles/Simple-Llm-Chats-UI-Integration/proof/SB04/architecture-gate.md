# SB04 C# Architecture Gate

Status: Pass

## Ownership

The LlmChats application contract owns the strongly typed `LlmChatOperationId?`. Runtime and EF persistence map their existing authoritative active-turn values into that contract. Web performs transport mapping only. No Razor or reusable UI project derives or stores the identity.

## Dependency direction

- No `.csproj`, `.props`, `.targets`, `.sln`, or `.slnx` file changed.
- Baseline snapshot: `snap-20260816171034-d26d371e`.
- Dependency query correlation: `code-analytics_861387a454f1457eb32144bb86ea6b05`.
- The scoped query reports the same two pre-existing AgentFramework module/type cycles recorded by SB03; neither involves a new edge from SB04.
- Application does not reference Web or UI. Web references the application contract only through its existing mapper boundary.

## Pattern decision

One nullable typed identity is the authoritative invariant. `HasActiveTurn` is derived, eliminating boolean/identifier disagreement without introducing a service, cache, facade, or new interface.

## Testability

The production engine can be tested with the existing in-memory conversation store; the production EF mapper is tested against PostgreSQL; the Web mapper is tested through HTTP; profile rejection and follower lifetime are tested at their owning application boundaries.

## Partial-class policy

No partial class was added or expanded.

## Review findings

| Severity | Finding | Decision |
|---|---|---|
| Info | Changing the engine-state positional value from `bool` to `LlmChatOperationId?` is a deliberate internal contract hardening | Accepted because representing `true` without an exact id would preserve an invalid state |
| Info | The HTTP contract retains `HasActiveTurn` and adds a nullable omitted member | Additive and backward compatible |
| Info | Existing Integration workspace has three unrelated baseline failures | Recorded in the required-suite transcript; no changed source participates in those failure paths |

## Closure decision

SB04 may close. Reopen it if the engine state, active-turn persistence mapping, conversation HTTP response, profile-fence return semantics, event-session lifetime, or transfer graph changes later.
