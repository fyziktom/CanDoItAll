# QA Prompt

Validate literal closure of all raw notes:

- Does the role editor preserve every executor-kind option used by current process role templates?
- Do step role assignments expose all supported responsibility kinds, including template vocabulary that previously fell back?
- Do artifact expectation controls expose all supported artifact kind and trust requirement values, including template vocabulary that previously fell back?
- Does a source audit prove unsupported template vocabulary is not silently accepted through fallback?
- Did the development DB cleanup touch only `Processes_` tables?
- Are agents, plugins, memory, projects, project structure, and related files preserved?
- Did current process templates reload and publish after cleanup?

UI proof should use the process editor route with the role, step, role-assignment, and artifact controls open. If browser proof is blocked, record the blocker and keep component tests as minimum proof.
