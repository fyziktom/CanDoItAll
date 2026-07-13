# Shared QA Prompt

```text
Review the completed subbundle against its README, the root requirements, and the phase gate.

Check for:
- missing raw requirement coverage;
- missing closure for analysis/04-live-repo-reentry-alignment.md;
- hidden direct references to native Cognitive Memory from generic memory, MAF, or base composition;
- hidden Qdrant dependency in base startup;
- hidden OpenAI/SemanticCompletion dependency in base startup where no provider is configured;
- duplicate operation handlers between tool and workflow executor;
- MAF memory integration that bypasses the current tool provider, workflow executor, or context contributor extension points;
- duplicate or incompatible source snapshot contract families;
- zero-provider paths that silently dispatch to native Cognitive Memory, Qdrant, OpenAI, or mock providers;
- missing timeout/cancellation/status paths for network provider calls;
- source adapters that leak EF entities or DbContext access;
- UI proof that only proves route load but not provider switching or fallback;
- tests that pass with stubs but do not exercise production emitters or dispatchers.

Reject the subbundle if proof is shallow, uncaptured, or downstream dependencies are unsafe.
```
