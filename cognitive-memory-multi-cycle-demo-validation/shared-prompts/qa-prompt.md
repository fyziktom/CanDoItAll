# QA Prompt

Review the execution as a source-backed memory validation, not just as an API smoke test.

Check these questions:

- Is every staged source file represented in the XLSX tracker?
- Was each stage loaded through APIs only?
- Was consolidation/dreaming forced after each stage?
- Were review decisions made from candidate previews and source excerpts?
- Are duplicates and contradictions explicitly classified?
- Do approved memories map to the correct project and source file?
- Are summaries useful, concise, and source-grounded?
- Is there cross-project leakage?
- Are vector/projection limitations explicitly recorded if vector checks cannot run?
- Do AI chat answers use Cognitive Memory rather than hidden prompt context?
- Are discovered defects converted into repair subbundles before final closure?

Reject closure if any stage has missing evidence, if chat validation was skipped without a blocker, or if wrong-source memories remain open.
