# Shared Implementation Prompt

```text
Implement the assigned subbundle only.

Read the root README, plan/01-phase-plan.md, requirements/01-normalized-requirements.md, architecture/01-target-solution.md, analysis/04-live-repo-reentry-alignment.md, and the assigned subbundle README before editing.

Preserve the dependency boundaries:
- generic memory code must not depend on native Cognitive Memory;
- MAF must not depend on native Cognitive Memory;
- base CanDoItAll startup must not require Qdrant or native memory;
- providers must use Source Gateway snapshots instead of AppDbContext access;
- tool and workflow executor calls must share the same operation handler;
- current MAF integration must use the existing tool provider, workflow executor, and context contributor extension points;
- source adapters must reuse, rehome, or explicitly migrate the existing MemorySourceSnapshot contract family;
- zero-provider configuration must work predictably without silently selecting native Cognitive Memory, OpenAI, Qdrant, or mock providers.

Use the smallest correct change set for this subbundle. Do not implement downstream phases early. Capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot honestly pass.

All source-code comments must be written in English.
```
