# SB07 Closure Semantic Invariants

## Documentation and runtime closure

- Invariant ID: `SB07-CLOSURE-001`
- Source raw note: update product and SharedInfo API/skill documentation, validate
  with `gpt-5.4-mini` only, rebuild, restart port 5032, and leave it ready for user
  testing.
- Expected behavior: documentation distinguishes in-process activity from durable
  run correlation and absent SSE; one low-cost provider call completes; validators,
  focused suites, build, architecture review, and live-host health have explicit
  outcomes.
- Disallowed shallow implementation: claim a public event stream that does not
  exist, use Terra or retry the paid call, mutate persisted configuration, hide a
  failed validator, or leave a stale/unhealthy host.
- Failing-first test: N/A for this process/non-production closure phase; behavior
  reds are owned and cited by the earlier governed subbundles.
- Passing test: `bundle://proof/SB07/transcripts/closure-validation.txt` records the
  final validation outcomes and `bundle://proof/SB07/runtime-closure.md` preserves
  the provider/run/host identities.
- Changed source files: product documentation is indexed by
  `bundle://proof/SB07/manifest.md`; sibling SharedInfo changes and generated OpenAPI
  provenance are recorded in `bundle://proof/SB07/runtime-closure.md`.
- Production assertions: durable `initialActivityOperationId` is correlation only;
  no current HTTP activity subscription/SSE endpoint is claimed; the managed port
  5032 host remains running after the rebuilt state is healthy.
- Red-team negative case: compare both live OpenAPI routes byte-for-byte, run both
  SharedInfo validators, search the updated skills for an unsupported SSE claim, and
  verify the live provider/model identity before and after the single call.
- Downstream dependency check: a future authorized SSE projection must consume the
  typed sequence/gap/partition contract without turning the transient stream into
  canonical storage or broadening API authorization.
