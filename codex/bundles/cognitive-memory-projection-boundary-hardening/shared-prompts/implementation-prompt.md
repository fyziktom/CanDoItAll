# Implementation Prompt

Use this prompt when assigning a subbundle to an implementation agent:

```text
Implement only the active subbundle from C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-projection-boundary-hardening.

Start by reading the bundle README, analysis, requirements, phase plan, traceability, and the active subbundle README. Do not implement Cognitive Memory. Keep RAG and SemanticCompletion generic; no Cognitive Memory-specific model names or policy fields may be added to those repos.

Make the smallest correct additive change set. Prefer strongly typed contracts over string expressions. Unsupported provider capabilities must fail predictably or be reported through typed capabilities; do not silently ignore filters, indexes, delete filters, or embedding profile gaps.

Update tests in the affected repo before claiming closure. Record exact commands and outcomes in reviews/01-execution-report.md. If live Qdrant is unavailable, provide mapper-level and contract-level proof plus an explicit validation note.

Stop and report a blocker if the active subbundle cannot satisfy its progression gate without changing the scope boundaries.
```
