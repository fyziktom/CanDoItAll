# Shared Prompt — Refactor Gate

```text
A refactor gate has fired. Do not continue feature work yet.

Steps:
1. Identify which source-of-truth, file-size or architecture rule was violated.
2. Create a new focused subbundle that repairs the issue.
3. Update the phase plan, dependency map and traceability references.
4. Implement and close the refactor subbundle first.
5. Re-run the failed proof that triggered the gate.
6. Only then resume downstream work.

Typical triggers:
- duplicate write path,
- direct dependency in the wrong direction,
- large mixed-responsibility service/page,
- second provider execution path,
- direct agent communication bypass,
- UI duplication between CRM-HR / Agents / Settings,
- scenario proof relying on shortcuts.
```
