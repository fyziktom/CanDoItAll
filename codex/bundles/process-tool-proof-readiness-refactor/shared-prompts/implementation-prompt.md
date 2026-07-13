# Implementation Prompt

Use this prompt when executing any subbundle in this bundle.

```text
You are implementing one subbundle from codex/bundles/process-tool-proof-readiness-refactor.

Before editing:
- read the root README, requirements, architecture guard files, phase plan, and the selected subbundle README;
- verify exact source references still exist;
- confirm prerequisites from earlier subbundles are complete.

Implementation rules:
- keep process/domain-specific tool and proof requirements in process contracts, templates, or process drivers;
- keep MAF generic: capability composition, metadata, and receipt observation only;
- do not add prompt-only enforcement for required proof;
- do not add large branches to existing partial classes or broad static policy classes;
- prefer small services with immutable records and typed reason codes;
- cache compiled step contracts by stable plan/step/readiness hash where repeated matching would otherwise rebuild data.

Validation rules:
- write focused unit tests for each new service;
- add integration tests for persistence, metadata, finalization, and template behavior when touched;
- include a negative test for the original artifact-only QA recheck failure mode;
- update reviews/01-execution-report.md with commands, results, proof artifacts, and architecture notes.
```
