# QA Prompt

```text
Review the implemented subbundle against the bundle constraints.

Check:
- Were exact source references respected?
- Did new code duplicate existing connector settings/schema concepts?
- Are secrets still vault-backed and resolved only through the runtime broker?
- Are plugin capabilities narrow and explicit?
- Did UI changes use the canonical renderer host/schema fallback?
- Did workflow executor catalog behavior remain backward compatible?
- Are disabled/unavailable executors rejected before runtime?
- Were helper classes placed in canonical modules rather than page-local files?
- Were tests and proof commands captured?
- Does the execution report contain enough evidence for a different agent to resume?
```
