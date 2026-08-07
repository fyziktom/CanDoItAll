# C# architecture gate

Status: Pending

## Required inputs

- current HEAD and diff
- changed C# and project files
- CodeAnalytics snapshot/dependency/cycle results
- responsibility and source-of-truth map
- test/build/guard transcripts
- subbundle proof manifests and handoffs

## Blocking checks

1. **Authority:** Is there one explicit grant owner and does runtime consume it?
2. **Scope:** Do all services/readers share one complete execution identity?
3. **Lifetime:** Is one owner responsible for one bundle/process host?
4. **Dependency direction:** Are contracts SDK/module-free and implementations wired outward?
5. **MAF boundary:** Are product/process semantics absent from MAF?
6. **State:** Are compatibility, migration, replay, and failure explicit?
7. **Approval:** Are decisions proposal-specific and durable?
8. **Lightweight LLM:** Is it agent-free, bounded, and failure-safe?
9. **Testability:** Can extracted behavior be tested without the old orchestration graph?
10. **No shallow refactor:** Did old grant/logic paths actually leave production?

## Result format

| Severity | Finding | Evidence | Required action |
|---|---|---|---|

### Closure decision

Pass | Blocked | Pass with explicitly non-blocking follow-up
