# Subbundle 06 — Typed output `RunAsync<T>` evaluation

## Goal

Evaluate whether any compile-time known agent flows should use MAF typed `RunAsync<T>` instead of dynamic `ResponseFormat` contracts.

## Context

The current dynamic `AgentStructuredOutputContract` path is appropriate for process automation because the output contract must be selected and persisted dynamically. Keep that path.

However, MAF also supports typed output for compile-time known flows. This may simplify smaller internal agent calls, test harnesses, or future non-process agents.

## Required work

1. Search the repo for direct internal agent calls that always expect a single compile-time DTO.

2. For each candidate, decide whether:

- dynamic `AgentStructuredOutputContract` should remain,
- typed `RunAsync<T>` would improve safety/readability,
- or no change is appropriate.

3. Document the decision in:

```text
docs/maf-runtime-stabilization.md
```

4. Do not rewrite the process-step automation path to typed `RunAsync<T>` unless the contract selection becomes compile-time known.

## Acceptance criteria

- Decision documented.
- No destabilizing refactor.
- Process automation still uses the dynamic contract path.
