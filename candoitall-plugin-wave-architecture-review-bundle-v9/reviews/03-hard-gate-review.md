Hard gate review:
The v9 gate package is intentionally stricter than v8 because v8 missed real unresolved blockers. The new gates focus on:
- repo-wide symbol retirement,
- active runtime fallback removal,
- manifest-driven UI proof,
- fake legacy enum persistence removal,
- read-path mutation removal.

MG-01 remains manual because absence-of-boundary is not robustly provable from one naming convention alone; it still must be signed off before external write-side plugins begin.
