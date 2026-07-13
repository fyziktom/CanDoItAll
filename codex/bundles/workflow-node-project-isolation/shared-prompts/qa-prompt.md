# Shared QA Prompt

Use this prompt for subbundle QA or independent review:

```text
Review the current subbundle against codex/bundles/workflow-node-project-isolation. Start from the raw requirements and traceability, then verify the current subbundle README, proof manifest, semantic invariants, transcripts, changed-file hashes, source assertions, and execution report rows.

Fail the gate if proof is prose-only, if a critical path lacks failing-first and passing evidence, if plugin executor behavior is only manifest-projected but not invoked, if MAF still owns default executor logic after adapter phases, or if UI/browser proof is missing for browser-visible adoption.
```

## QA Questions

- Did the subbundle preserve existing workflow definitions, executor ids, template keys, and API payloads?
- Did moved code reduce coupling, or did it only rename the old MAF/Core hub?
- Are plugin grants, OAuth/secrets, host commands, source/trust metadata, side effects, and deterministic preview still correct?
- Are errors explicit and actionable?
- Are tests scoped to the new project boundaries?
- For UI changes, did the agent review the screenshot against readability, clipping, spacing, layering, and data correctness?

## Hard Failure Conditions

- Missing proof manifest for a completed critical subbundle.
- Missing semantic invariant contract for a completed critical subbundle.
- Missing negative proof for a behavior-changing critical subbundle.
- New abstraction project references MAF/Web/Modules without an explicit exception.
- Plugin executor disappears from catalog instead of becoming unavailable with reason.
- Run Preview uses production external mutation paths.
- Host registration still manually wires each workflow service after composition extraction is complete.
