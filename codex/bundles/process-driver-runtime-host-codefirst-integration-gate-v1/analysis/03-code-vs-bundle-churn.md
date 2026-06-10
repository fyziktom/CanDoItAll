# Code vs bundle churn analysis

## Problem
The latest implementation still produced a very large amount of files under `codex/bundles/...` compared with a small set of changed production/test files.

Examples of current churn pattern:

- Many generated `subbundles/SBxx/README.md` files.
- Many proof manifests and transcript placeholders.
- Execution reports with many rows but limited corresponding source changes.

## Required correction
The next bundle must be executed as a code-first change:

1. Use fewer, larger subbundles.
2. Change real production/test code in each implementation phase.
3. Use critical proof manifests only for gates.
4. Track diff ratio using `git diff --stat` and `git diff --numstat`.
5. Fail final closure when bundle/proof changes dominate.

## Ratio gate
At final closure, Codex must record:

```powershell
git diff --numstat <base> HEAD
```

and compute:

- production/test/docs changed lines under `src/`, `tests/`, `docs/`
- coordination/proof changed lines under `codex/bundles/`

Required:

- `(src + tests + docs changed lines) / codex-bundle changed lines >= 2.0`
- or, when proof files must be larger due to transcripts, a human-readable exception must list exact transcript files and show production/test files still dominate meaningful implementation.

No exception is allowed for duplicated subbundle boilerplate.
