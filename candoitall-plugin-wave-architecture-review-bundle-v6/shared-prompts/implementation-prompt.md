# Implementation Prompt

Implement the subbundle exactly as written.

Rules:

- preserve node as the universal carrier
- preserve semantic X/Y and markers as canonical
- do not reintroduce persisted projection truth
- do not solve extensibility by adding more enums and switch blocks
- add or update tests for every changed invariant
- when changing lifecycle semantics, preserve existing node identity unless the subbundle explicitly says otherwise
- collect proof artifacts required by the acceptance checklist before closing the subbundle
