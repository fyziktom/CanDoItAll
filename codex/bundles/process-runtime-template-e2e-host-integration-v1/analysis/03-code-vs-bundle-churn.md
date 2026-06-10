# Code vs Bundle Churn

## Problem
Several previous bundles generated large `codex/bundles` trees and many repeated subbundle README/proof files while making only small production-code changes. This slows progress and makes it look like the system advanced more than it did.

## New policy
This bundle uses 8 larger subbundles and an implementation-ratio closure gate.

Final closure must compute grouped line changes from:

```powershell
git diff --numstat <start-sha>...HEAD
```

Groups:

- `src/`
- `tests/`
- `docs/`
- `codex/bundles/`

Blocking ratio:

```text
(src + tests changed lines) >= 4 × codex/bundles changed lines
```

Docs do not count as implementation for this ratio. They may support the work but cannot mask weak source/test progress.

## Additional guard
During execution, Codex must not create new per-subtask boilerplate folders beyond the existing bundle structure unless a validator requires it. Critical proof may be concise and centralized.
