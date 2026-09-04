# Agent Tool Execution Integrity: Ollama and Shared Providers

Implementation is complete. The reported defect was reproduced from run `894e1404-3019-4221-8be6-7769c0f472ae`, repaired above the provider boundary, and verified through both direct Ollama and a temporarily published shared-provider route.

The repair gives malformed calls bounded field-level feedback, records typed invocation and effect outcomes, refuses false terminal success for unresolved mutations, carries trusted scoped evidence into later turns, publishes durable receipts through persistence/API, and refreshes only the matching project after a committed effect. MAF was upgraded to 1.20 as a coherent dependency family; the upgrade improved the baseline but did not itself fix application-owned outcome handling.

Live shared-provider testing exposed two additional transport defects. Ollama's OpenAI-compatible endpoint rejects boolean JSON Schema nodes generated inside tool properties, so the Ollama relay now rewrites only those schema nodes to equivalent object schemas. After the first tool executed, the source request policy rejected MAF's valid `content: ""` assistant tool-call message; it now accepts that contentless form only when an assistant message contains tool calls.

## Outcome

- Direct live run `a1e4d57b-b84b-4eb1-8cea-432cddf13861`: completed with the asset receipt `Succeeded / Committed`; the open canvas refreshed from 3 to 4 nodes without reload.
- Shared live run `b1b2ead6-09bc-4248-b007-d4bb74cfa30c`: completed with two tool calls and the asset receipt `Succeeded / Committed`; the same open canvas refreshed from 4 to 5 nodes without reload.
- Original incident run and data were preserved. Port 5032 remains stopped.
- Temporary shared publication was removed, its import retired, its source disabled, and Portfolio Architect restored to the direct Ollama profile. Automatic approval review rejected deletion of the disposable proof project, so it remains available for inspection.

## Evidence

Start with [the execution report](reviews/01-execution-report.md), [requirement traceability](traceability/01-requirement-traceability.md), and [closure proof](proof/closure-summary.md). Live run JSON, catalog snapshots, and screenshots are under `proof/SB06/live/`.

## Validation Summary

- Bundle preparation status: `Completed`
- Execution status: `Completed`
- Subbundle gate review: `Pass` — SB00-SB06 completed
- Final closure gate: `Pass` — portability, documentation, governed proof, completed-bundle and diff gates passed
- C# architecture gate: `Pass`
- Focused unit: 154 passed
- Focused integration: 37 passed
- Focused components: 5 passed
- Production and stable test-solution Release builds: passed, zero warnings/errors
- Frozen stable suite: 9,525 of 9,526 passed; its sole unrelated timing threshold passed immediately in isolated rerun
- Final documentation, bundle, governed-proof, diff, and portability-static gates: recorded in the execution report