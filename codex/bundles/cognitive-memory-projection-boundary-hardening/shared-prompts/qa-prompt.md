# QA Prompt

Use this prompt for review after each subbundle:

```text
Review the active subbundle in C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-projection-boundary-hardening.

Check that the implementation matches the normalized requirements and did not drift into Cognitive Memory implementation. Verify that new contracts are strongly typed, provider-neutral, and covered by tests. Confirm unsupported capability behavior is explicit and tested.

For RAG work, inspect filter validation, Qdrant mapper translation, payload index behavior, delete-by-filter/source cleanup, and capability flags. For SemanticCompletion work, inspect stable embedding profile metadata and compatibility with existing consumers.

Review command output recorded in reviews/01-execution-report.md. Browser proof is N/A unless the implementation unexpectedly changes a UI or host-visible sample, in which case require appropriate proof before closure.

If any downstream Cognitive Memory phase would still need direct Qdrant calls, unscoped projection search, post-filtered global vector results, or re-derived embedding profile ids, mark the subbundle failed and reopen the relevant phase.
```
