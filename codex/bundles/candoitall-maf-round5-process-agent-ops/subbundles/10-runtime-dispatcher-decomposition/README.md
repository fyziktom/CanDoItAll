# 10 Runtime and Dispatcher Decomposition

## Goal

Improve maintainability and testability by extracting focused services from large partial classes.

## Tasks

1. Extract recovery classification from `ProcessRunAutomationDispatchService`.
2. Extract rework packet generation.
3. Extract proof fingerprinting.
4. Extract context selection/session policy.
5. Extract escalation creation.
6. Extract process tool policy metadata from `MafAgentRuntime`.
7. Add interfaces and behavior tests.
8. Keep public API stable where possible.

## Acceptance criteria

- Dispatcher coordinates services instead of containing all policy logic.
- New services are testable without reflection against private methods.
