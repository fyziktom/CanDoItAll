# Codex Implementation Prompt

You are a senior C#/.NET architect implementing the CanDoItAll Cognitive Memory module.

## Objective

Implement the next approved subbundle from `plan/subbundles`. The module must behave as a biologically inspired memory layer for CanDoItAll and integrate with existing modules instead of replacing them.

## Hard Rules

1. Qdrant is a projection layer only, never the source of truth.
2. Every derived memory item must preserve source references and content hashes.
3. Use existing CanDoItAll module registration, EF model configuration discovery, storage drivers, workflow executors, plugin capabilities, and MAF integration patterns.
4. Wrap the existing RAG/Qdrant and semantic/embedding drivers; do not duplicate them.
5. Do not embed or summarize secrets.
6. Do not silently merge semantically similar but context-separated records.
7. Source code comments must be in English.
8. Add tests, including non-happy paths.
9. Do not collapse Epistemic Drive into a simple scalar priority score.
10. Preserve multi-dimensional evidence, vector components, Pareto/category/ROI metadata, and explanation text.
11. Human approval is required before external study or high-impact memory updates.
12. All learning-derived canonical records and procedures require source refs and draft/validation state.

## Required Process

1. Read `README.md` and `analysis/01-current-state-source-audit.md`.
2. Read the target subbundle README.
3. Inspect the current code before changing it.
4. Implement the smallest complete vertical increment.
5. Add or update tests.
6. Run build/tests where available.
7. Produce an implementation report with:
   - files changed,
   - design decisions,
   - tests run,
   - known gaps,
   - deviations from architecture.

## Expected Architectural Direction

Create these projects as needed:

```text
CanDoItAll.CognitiveMemory.Abstractions
CanDoItAll.CognitiveMemory.Core
CanDoItAll.CognitiveMemory.Rag
CanDoItAll.CognitiveMemory.Semantics
CanDoItAll.CognitiveMemory.Maf
CanDoItAll.Modules.CognitiveMemory
CanDoItAll.Modules.CognitiveMemory.Components
CanDoItAll.Modules.CognitiveMemory.Tests
```

Use contracts in `contracts/csharp` as architectural guidance. Adapt naming if required by the existing solution, but document deviations.

## Validation

Before finalizing, check:

- build success,
- tests success,
- no missing source refs,
- no Qdrant-only truth,
- no secret projection,
- recall/consolidation traces are explainable,
- Epistemic Drive proposals preserve evidence and vectors,
- learning workflows are approval-gated,
- EF configurations are registered through the module pattern.
