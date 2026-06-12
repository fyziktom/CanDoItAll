# QA Prompt

```text
QA this subbundle against the bundle, not against Codex's execution report alone.

Checklist:
- Confirm raw user requirements are covered.
- Confirm exact files changed match the subbundle scope.
- Confirm forbidden transient tracked paths are absent where relevant.
- Confirm no SB/INV/bundle/subbundle naming leaks remain in active tests outside allowed bundle-skill tooling.
- Confirm MAF has no compile-time reference to CanDoItAll.Modules.Processes.
- Confirm Process Core remains deterministic and dependency-clean.
- Confirm domain drivers remain verification-only and do not mutate process/workspace/storage or call external systems.
- Confirm software-delivery proof rules are not directly owned by generic dispatcher partials after SB03.
- Confirm gateway remains explicit typed read-only dispatch, not generic runtime hosting.
- Confirm tests/build/scans listed in the subbundle were actually run and results are recorded.
- Confirm any blocked live smoke proof is reported honestly with exact reason.

Reject closure if proof is missing, if tests are skipped without owner/reason/reopen trigger, or if temporary work-package artifacts/names remain tracked.
```
