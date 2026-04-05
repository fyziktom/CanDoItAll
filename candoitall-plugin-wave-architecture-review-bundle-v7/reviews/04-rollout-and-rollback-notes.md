# Rollout and rollback notes

## Safe rollout order

1. land the canonical-model refactor behind tests
2. pass the hard-gate script
3. run build/test in a real .NET environment
4. only then start the connector/plugin wave

## Rollback view

If the refactor causes instability:
- keep the new guardrail tests
- keep the hard-gate script
- revert implementation slices carefully, not the guardrails
