# Build Benchmark Findings

## Current Build Benchmarks

From `artifacts/build-bench/summary.json`:

| Scenario | Shape | Elapsed |
| --- | --- | ---: |
| normal-warm | normal outputs, default env | 54.980s |
| managed-exact | isolated artifacts, restore, server off, markers | 68.955s |
| improved-candidate | normal outputs, `--no-restore`, server on | 22.286s |

## Important Correction

The binlogs show `dotnet build` is already invoking MSBuild with `-maxcpucount`. So chasing `-m` is not the main answer here. The real differences are:

- restore behavior
- isolated artifacts output
- MSBuild server disabled

## Factor Isolation

From `artifacts/build-factor/summary.json`:

These runs were executed later on an even warmer machine state than the first benchmark set. Use them for relative impact, not as direct replacements for the earlier absolute times.

| Scenario | Elapsed | What it shows |
| --- | ---: | --- |
| `noartifacts-serveroff` | 25.523s | warm build with restore and server off |
| `noartifacts-serveroff-norestore` | 17.133s | restore alone costs about 8.4s here |
| `noartifacts-serveron-norestore` | 14.393s | MSBuild server saves about 2.7s on this warm no-restore build |
| `artifacts-serveroff-norestore` | failed in 2.634s | isolated artifacts path cannot use existing assets without restore |

## Why `--artifacts-path` Hurts the Build Lane

The isolated artifacts no-restore run failed with:

- `NETSDK1004`
- missing `project.assets.json` under the isolated artifacts `obj` tree

This is a major architectural point:

- If each managed build uses its own isolated artifacts tree, it cannot cheaply reuse the normal warmed restore state.
- That forces either restore work or a separate cache-warming scheme for every isolated tree.
- This means the current managed build design is structurally hostile to a fast no-op or near-no-op inner loop.

## Binlog Highlights

### Normal warm

- elapsed: `54.18s`
- `ResolveAssemblyReference`: `5.145s`
- `Csc`: `15.290s`
- `ResolveProjectReferences`: `366.228s` cumulative target time

### Managed exact

- elapsed: `67.79s`
- `RestoreTask`: `2.724s`
- `ResolveAssemblyReference`: `10.130s`
- `Csc`: `47.814s`
- `ResolveProjectReferences`: `322.796s` cumulative target time

### Improved candidate

- elapsed: `20.97s`
- no restore task
- `ResolveAssemblyReference`: `0.540s`
- no meaningful `Csc` work in the replay, indicating a much closer no-op shape

## Build Direction

The fastest near-term win is not "parse logs better." It is "stop changing the build into a slower shape in the first place."

Recommended default build policy for inner-loop managed builds:

- normal project outputs
- `--no-restore` when restore state is already warm
- MSBuild server enabled
- log filtering layered on top of the fast build, not substituted for it
