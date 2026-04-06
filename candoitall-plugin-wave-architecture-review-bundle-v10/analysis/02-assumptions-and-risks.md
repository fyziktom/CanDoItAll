# Assumptions and risks

## Review basis
This review used:
- the current repository snapshot,
- the phase9 bundle included inside the repo,
- the attached skillset,
- the previous v6/v7/v8 bundles to understand repeated misses.

## Validation scope
Static source inspection was sufficient to invalidate the bundle9 closure claim, because the write operations are directly visible in active production code.

The current container does **not** provide the .NET SDK, so the full `dotnet build` / `dotnet test` matrix was not rerun here. Phase10 therefore requires Codex to re-run runtime validation in the target .NET environment and attach the output.

## Main risk if phase10 is skipped
If the team starts the plugin wave while read paths still mutate persistence:
- stale system-managed rows and stale layout overrides can disappear during normal user reads,
- future plugin contributors can accidentally rely on read-time cleanup instead of explicit repair,
- concurrency and cache behavior become harder to reason about,
- another narrow static gate can turn red/green independently of the actual runtime behavior.

## Secondary guarded-rollout risks
These do not block phase10 by themselves, but they remain visible:
- marker compatibility fallback from legacy metadata is still active in read-side runtime composition,
- node-reference compatibility fallback from metadata is still active when reference rows are absent,
- manifest-driven editor coverage still lacks unknown-plugin regression proof across all field types.
