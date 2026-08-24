# Repository baselines

## CanDoItAll

- Repository: `fyziktom/CanDoItAll`
- Prepared branch: `development`
- Prepared commit: `1625b336e4f60ddb64987240c3a3dc485591d20f`
- Inspected Git tree: `da6da849abd3dd7b9895431e92c6a2e0c9b8e4da`
- Runtime: .NET 10
- Main application database: PostgreSQL
- Root product graph: `CanDoItAll.slnx`
- Focused test graphs:
  - `tests/Solutions/CanDoItAll.Tests.Unit.slnx`
  - `tests/Solutions/CanDoItAll.Tests.Components.slnx`
  - `tests/Solutions/CanDoItAll.Tests.Integration.slnx`
  - `tests/Solutions/CanDoItAll.Tests.Playwright.slnx`
  - `tests/Solutions/CanDoItAll.Tests.Stable.slnx`
- Development Docker stack: `compose.yaml`

## CanDoItAll.SharedInfo

- Repository: `fyziktom/CanDoItAll.SharedInfo`
- Prepared branch: `main`
- Prepared commit: `053f8b356fbc8a28bf822e0a051c25804bd81b65`
- Bundle conventions: `codex/skills/bundles`
- Shared OpenAPI source: `codex/skills/_candoitall-api-shared`
- Existing API skill pattern: `codex/skills/candoitall-api-*`

## Re-entry rule

Before SB00 and again before any subbundle touching a changed area:

1. capture current branch, commit, and working-tree status;
2. compare current project layout, symbol owners, API conventions, and test guidance with the
   evidence in this bundle;
3. record changed assumptions;
4. reopen only the affected architecture decisions;
5. never reset or overwrite the operator's newer code.

A changed baseline is not a reason to abandon the bundle. It is a reason to update the narrow
source map and proof plan.
