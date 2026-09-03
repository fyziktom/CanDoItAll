# Shared Implementation Prompt

Implement the current subbundle only.

Before editing:

1. read root and local repository instructions,
2. verify branch, HEAD, remotes, and worktree,
3. run the v2 scope guard where the subbundle touches CanDoItAll,
4. inspect current source rather than relying on recorded line numbers,
5. state the smallest coherent implementation boundary.

During implementation:

- preserve current development semantics,
- keep source ownership boundaries,
- prefer typed Blazor/component contracts,
- retain accessibility and stable test selectors,
- avoid generated-file hand editing,
- do not weaken tests,
- do not import v2,
- add English comments only where they clarify a non-obvious contract.

After implementation:

- run the subbundle's targeted gate,
- inspect the complete diff,
- update the execution report with commands/results,
- commit only if the invoking workflow permits it,
- stop at the progression gate.
