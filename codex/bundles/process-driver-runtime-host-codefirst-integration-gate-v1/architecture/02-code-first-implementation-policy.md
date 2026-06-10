# Code-first implementation policy

## Why this exists
The last two implementation waves spent too much diff on bundle/proof scaffolding. This bundle must force substantial code changes while still keeping architecture safe.

## Diff ratio gate
At final closure, Codex must attach a compact `git diff --numstat` summary and classify changed lines into:

- `source`: `src/**`
- `tests`: `tests/**`
- `docs`: `docs/**`, excluding `codex/bundles/**`
- `bundle`: `codex/bundles/**`

Required ratio:

```text
(source + tests + docs) >= 2 * bundle
```

If this fails, the bundle is incomplete.

## Bundle artifact discipline
Allowed proof artifacts:

- execution report,
- critical gate manifests only,
- command transcripts for build/test/source scans,
- final handoff.

Not allowed:

- 30+ boilerplate subbundle readmes during execution,
- repeated copied semantic proof text,
- large new planning-only directories,
- report-only closure.
