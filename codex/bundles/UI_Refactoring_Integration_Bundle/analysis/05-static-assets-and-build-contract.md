# Static Assets And Build Contract

## Required source-mode invariant

Immediately after cloning the three repositories at the selected commits, this command graph must
have all host-referenced static assets without a prior undocumented npm command:

```text
dotnet restore/build/publish CanDoItAll
  -> project reference CanDoItAll.Components.BaseLib
  -> static web asset css/material-symbols.css exists
  -> static web asset css/output.css exists
```

The source checkout itself must satisfy the invariant.

## Why application-only CI generation is insufficient

Adding a one-off npm command to the CanDoItAll CI would leave these consumers inconsistent:

- local source-reference developers,
- Docker source-context builds,
- other sibling source consumers,
- IDE builds,
- package builds.

The asset is owned and redistributed by Components, so Components must make its source consumer
contract deterministic.

## Prescribed policy

Commit:

```text
src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css
```

Ignore:

```text
samples/**/wwwroot/output*.css
assets/output-test.css
other transient Tailwind outputs
```

Verification pattern:

```powershell
npm ci
npm ci --prefix Tailwind
npm run build:tailwind
git diff --exit-code -- `
  src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css
```

The first commit after policy change may add the generated file. Subsequent CI runs must fail on
drift.

## CanDoItAll host checks

- `App.razor` references `material-symbols.css`.
- `App.razor` references BaseLib `output.css`.
- a source checkout assertion confirms both files exist in the sibling repository,
- browser proof confirms both URLs return 200,
- no icon fallback text appears on representative pages,
- Docker publish includes both assets.

## Package checks

Inspect packed BaseLib `.nupkg` and assert it contains:

```text
staticwebassets/.../css/material-symbols.css
staticwebassets/.../css/output.css
```

Use the package's actual static-web-assets layout; do not hard-code a ZIP path before inspecting
the generated package.
