# QA Prompt

Review the completed subbundle against:

- objective
- exact source references
- acceptance checklist
- proof required
- downstream dependency impact

Reject the closure if:

- a second canonical truth still exists
- node semantics leaked into metadata or plugin-specific ad-hoc fields
- hierarchy is still duplicated
- assignment semantics are still hardcoded in scattered switches
- plugin extensibility still depends on expanding enums
