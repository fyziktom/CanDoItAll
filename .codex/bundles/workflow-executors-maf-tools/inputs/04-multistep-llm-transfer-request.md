# Multi-Step LLM Transfer Follow-Up

## Raw Request

The scenarios were moving in the right direction, but many visible examples only had a single executor step. Add multi-step examples that prove executors work together with LLM calls and that workflow inputs/outputs transfer correctly between nodes.

The required example shape is:

1. Read something from project structure.
2. Transform it through an LLM call.
3. Save the transformed result back to project structure.

Also include equivalent evidence for other useful executor chains where practical.

## Interpretation

- Single executor smoke tests are not enough for closure.
- The MAF compiler must execute LLM nodes instead of passing payloads through.
- Downstream executor settings need a deliberate way to use upstream payload content, especially for writing files or creating project-structure assets.
- PostgreSQL scenario proof must include real executor -> LLM -> executor runs, not only unit-test doubles.
