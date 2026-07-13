# QA Prompt

Use this prompt when validating a completed subbundle or final bundle closure.

```text
Review this work as a C# architecture and process-runtime gate.

Check these failure modes first:
- required current-run receipts can still be replaced by upstream artifacts;
- HR readiness can still miss required or suppressed tools, skills, or MCPs;
- MAF contains new software-delivery, QA, image-analysis, or project-management domain instructions;
- fallback still retries artifact-only recovery for missing proof;
- process template requirements remain prose-only;
- large classes or partials grew with new policy branches instead of extracted services;
- tests prove only happy paths.

Required proof:
- source references for changed contracts, compiler/evaluator/gate/fallback/template code;
- targeted dotnet test transcripts;
- negative proof for missing required receipts;
- E2E browser/image receipts in the final template phase;
- dependency direction proof that MAF stays process-agnostic.
```
