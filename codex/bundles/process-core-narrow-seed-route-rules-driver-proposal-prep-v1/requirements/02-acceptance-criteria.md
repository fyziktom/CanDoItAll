# Acceptance Criteria

AC-001: `CanDoItAll.Processes.Core` exists only if the bundle moves the narrow route pure-rule family.

AC-002: Core project builds independently with allowed dependencies only.

AC-003: Architecture tests fail if Core references forbidden assemblies/namespaces.

AC-004: Existing route stage order and eligibility behavior is preserved by unit tests.

AC-005: Process module route pipeline keeps the same order and behavior.

AC-006: No route handler, route service, claim lifecycle, transition execution, finalizer application, AgentFramework execution, EF query, storage/file IO, or workspace behavior enters Core.

AC-007: Driver readiness remains proposal-only; production source scan rejects driver tokens.

AC-008: Full solution build passes.

AC-009: Full unit tests pass, or any unrelated failures are separately documented with proof they are unrelated.

AC-010: Focused dispatch/route integration tests pass.

AC-011: Source scans prove no UI/media drift.

AC-012: Final red-team explicitly decides whether a second Core family can be proposed next.
