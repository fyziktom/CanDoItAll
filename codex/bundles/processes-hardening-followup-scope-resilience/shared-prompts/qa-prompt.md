# Shared QA Prompt

Review the implementation against the original user problem:

1. Can an architecture/design/planning step still mutate product files or start implementation work?
2. Can a workflow-backed process role complete without process artifact expectations being loaded and validated?
3. Can a subprocess parent step complete with a source-less placeholder artifact?
4. Does a review/QA decision with a repair/no-go branch select the branch instead of hard blocking?
5. Can a downstream step blocked/waiting for upstream artifact materialization resume after the source artifact appears?
6. Can generic process artifacts be falsely rejected because they contain words like `todo`, `not available`, `decision log`, or `markdown`?
7. Does artifact lineage ensure the satisfying artifact belongs to the current run/attempt/workflow/recovery path?
8. Does retry compression prevent repeating the same no-progress attempt?
9. Does process definition lint catch ambiguous or over-broad step definitions?
10. Are tests generic and not only Blazor/.NET focused?

Reject the implementation if proof is source-assertion-only. Critical behavior must be exercised by production emitters or realistic integration tests.
