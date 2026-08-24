# Test budget and gates

## Routine command shape

Example only; use current paths:

```powershell
dotnet build <affected-production.csproj> -c Release --no-restore /m:1
dotnet test <owning-test.slnx-or-csproj> -c Release --no-build --no-restore `
  --list-tests --filter "FullyQualifiedName~SharedProviderCatalog"
dotnet test <owning-test.slnx-or-csproj> -c Release --no-build --no-restore `
  --filter "FullyQualifiedName~SharedProviderCatalog"
```

Record command, exit code, duration, expected discovery, actual discovery, pass/fail/skip.

## Restoration/build rules

- restore once after project/package graph changes;
- do not repeat restore for source-only edits;
- rebuild the changed production project before using `--no-build`;
- build test assembly after adding/changing tests;
- `/m:1` for broad/final stability and when repository guidance requires it;
- no three separate builds of the same app image for central/client-a/client-b.

## Subbundle budgets

| SB | Normal maximum | Special lane |
| --- | --- | --- |
| SB00 | 3 production builds, 2 filtered topics | CodeAnalytics/reference audit |
| SB01 | 3 builds, 3 focused topics | none |
| SB02 | 4 builds, 3 focused topics | migration/model check |
| SB03 | 3 builds, 3 focused topics | Web host only |
| SB04 | 4 builds, 3 focused topics | streaming fixture |
| SB05 | 3 builds, 3 focused topics | scripted HTTP only |
| SB06 | 4 builds, 3 focused topics | composition smoke |
| SB07 | 2 image builds max cumulative, one Docker lane | first 3-app run |
| SB08 | 3 builds, 2 component topics | no Playwright |
| SB09 | 2 builds, 2 component topics | one focused Playwright run |
| SB10 | docs/tool validators | no Docker rerun by habit |
| SB11 | Web/OpenAPI/SharedInfo validators | one snapshot capture |
| SB12 | frozen final build | one stable aggregate + one clean Docker lane |

## Final stable trigger

The feature necessarily changes:

- project references;
- Web composition;
- API authorization;
- access-context middleware;
- EF entities/migration;
- public API/OpenAPI.

Therefore SB12 owns one stable aggregate after all production code, tests, docs, and SharedInfo
are frozen.

No earlier subbundle may run it "for safety."

## Docker image budget

- build application image once in SB07 after backend freeze;
- rebuild at most once in SB12 if UI/final code changed;
- tag with source commit/worktree hash;
- central/client-a/client-b use the same tag;
- deterministic upstream image may be built separately and cached.

## Failure handling

A failing focused test is repaired and rerun. Do not respond by running a broader suite.
A discovery count mismatch is investigated before execution. A flaky test must be made
deterministic or explicitly blocked; repeated blind retries are not evidence.
