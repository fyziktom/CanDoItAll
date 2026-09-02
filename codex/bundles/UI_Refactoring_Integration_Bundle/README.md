# CanDoItAll UI Refactoring Integration Bundle

**Bundle status:** Ready for execution  
**Prepared:** 2026-09-01  
**Primary implementation branch:** `fyziktom/CanDoItAll:ui-refactoring`  
**Forbidden branch:** `fyziktom/CanDoItAll:ui-refactoring-v2`  
**Repositories:** `CanDoItAll`, `CanDoItAll.Components`, `CanDoItAll.FileTools`

## Objective

Integrate the original UI-refactoring work into the current CanDoItAll product line without
importing the separate second-generation redesign. The execution must:

1. stabilize the already-merged `CanDoItAll.Components/main`,
2. preserve source-reference development across all three sibling repositories,
3. verify that FileTools remains independent from Components,
4. merge current `CanDoItAll/development` into `CanDoItAll/ui-refactoring`,
5. resolve the original branch's five intended deltas against today's application,
6. adapt current application call sites to the merged Components contract,
7. normalize and raise package versions across the three repositories,
8. prove source mode, package mode, browser behavior, and container behavior,
9. leave merge-ready branches for `development` and then `main`.

## Critical current findings

- The branch names on GitHub are `ui-refactoring` and `ui-refactoring-v2`, not
  `ux-refactoring`.
- The original application branch has only five unique commits. Its intended deltas are:
  `.idea/` ignore, a root watch command, Material Symbols host asset, an old SDK pin change,
  and Podman/macOS documentation.
- `CanDoItAll/development` is much newer than the original branch. Preserve current
  development behavior and apply only the intentional original deltas.
- The Components merge is not release-ready at the recorded baseline. Its current `main`
  CI failed three approval/asset-manifest tests.
- Components now ignores the distributed BaseLib `output.css`, although CanDoItAll consumes
  Components by sibling project reference by default. A clean source checkout therefore has
  no guaranteed BaseLib stylesheet. This bundle makes the distributed BaseLib CSS a committed,
  deterministically verified source asset; sandbox CSS remains generated.
- FileTools does not directly depend on Components. Its package validation explicitly forbids
  that dependency. Do not add one.
- FileTools package version declarations are inconsistent. Normalize them to one repository
  version.
- Current raw `.material-icons` usages in CanDoItAll must move to the stable
  `.cda-material-icon` contract or the `<Icon>` component.
- The root `PODMAN.md` from the old branch is stale because it says sibling Components and
  FileTools repositories are unnecessary. Today's default source-reference mode requires them.

## Hard boundaries

1. **Never merge, cherry-pick, copy, or manually recreate work from `ui-refactoring-v2`.**
2. Do not add the v2 application toolbar, theme system, navigation redesign, canonical URL
   proposal, design documents, or page rewrites.
3. Do not use v2 as a conflict-resolution source.
4. Do not introduce a Components dependency into FileTools.
5. Do not mix source-reference and package-reference modes within one restore/build/test graph.
6. Do not approve generated API or asset snapshots without reviewing the semantic diff.
7. Do not hide missing static assets by removing host links or weakening tests.
8. Do not publish packages or merge remote protected branches unless the invoking user
   explicitly authorizes those write operations.
9. Keep changes focused on the original UI-refactoring integration. Record unrelated defects;
   do not absorb them opportunistically.
10. Comments added to source code and scripts must be in English.

## Recorded source baselines

These are discovery baselines, not immutable execution pins. Refresh all remote tips before
editing and record any movement in the execution report.

| Repository / branch | Recorded SHA | Recorded state |
|---|---|---|
| `CanDoItAll/development` | `7e2a3005cd3aa202badef72fdf4ee310800958aa` | Green CI at discovery |
| `CanDoItAll/main` | `fec027a59bd48cc1e08c407f465f0f1a0ae1029c` | Development merge; CI was running |
| `CanDoItAll/ui-refactoring` | `a2903c400cc35e6d1d2f233c51e73feb256ce2aa` | Original branch to integrate |
| `CanDoItAll/ui-refactoring-v2` | `7b7d3639a41eb90147d58f53db1bf19de55b2df5` | Explicitly forbidden |
| `CanDoItAll.Components/main` | `38c3072fc4fe18c6f6b1e770f4242a3445d80ada` | UI merge present; CI failed |
| `CanDoItAll.FileTools/main` | `cc398d4e47696188d15c177c62faf42e937b4f7e` | Green CI at discovery |
| `CanDoItAll.FileTools/development` | `c95dd07208a6d48724443317cdc6cfe67a13020a` | Older than FileTools main |

## Required workspace layout

The default commands assume sibling repositories:

```text
<workspace>/
  CanDoItAll/
  CanDoItAll.Components/
  CanDoItAll.FileTools/
```

Do not reclone repositories that are already present. Do not use a global package cache as proof
that a clean checkout works.

## Execution order

| Order | Subbundle | Gate |
|---:|---|---|
| 1 | `01-freeze-scope-and-guard-v2` | Exact branches, baselines, and v2 denylist recorded |
| 2 | `02-stabilize-components-main` | Components full CI-equivalent path is green |
| 3 | `03-normalize-package-versions` | One unused coordinated version is applied and packed |
| 4 | `04-validate-filetools-independence` | FileTools remains independent and all nine packages validate |
| 5 | `05-merge-development-into-ui-refactoring` | Current development merged without v2 contamination |
| 6 | `06-adapt-candoitall-to-components-main` | Application builds; icon/static-asset contracts are migrated |
| 7 | `07-refresh-source-pins-and-operations-docs` | CI pins and modern Podman docs match the final sources |
| 8 | `08-cross-repo-build-test-browser-proof` | Source, package, browser, and container gates pass |
| 9 | `09-merge-closure-development-main` | Merge-ready history and final ancestry report produced |

Each subbundle is independently restartable. Follow its prerequisites and reopen triggers.

## Package version policy

Use a single coordinated stable version `V` for:

- all publishable Components packages,
- all nine FileTools packages,
- the two package fallback properties in CanDoItAll.

The recommended starting candidate is **`0.3.0`** because it is greater than the committed
FileTools project values observed up to `0.2.1` and represents a pre-1.0 compatibility boundary.
Before editing, query every configured public/private feed. If `0.3.0` already exists for any
package in either family, choose the next unused coordinated stable version and document why.

Package publishing is not part of this bundle. Local pack and local-feed consumption proof are
required.

## Definition of done

The bundle is complete only when all of the following are true:

- Components `main`-equivalent work is green, including approval tests and package asset checks.
- A clean Components checkout contains the exact BaseLib CSS loaded by source consumers, or an
  equally robust replacement is implemented and proven across local, CI, and Docker source mode.
  The prescribed solution is to commit and deterministically verify BaseLib `output.css`.
- FileTools has no Components dependency and all nine packages use version `V`.
- `CanDoItAll/ui-refactoring` contains current `development` plus only the original intended
  branch work and integration fixes.
- No unique `ui-refactoring-v2` commit is an ancestor of the integration branch.
- No `.material-icons` implementation selector remains under application `src`, `tests`, or
  application Tailwind sources unless documented as an intentional third-party compatibility
  exception.
- CanDoItAll loads `material-symbols.css` and the BaseLib output stylesheet without 404s.
- Current CI pins reference the exact final Components and FileTools commits.
- Source-reference mode and package-reference mode both restore, build, and pass the selected
  tests from clean outputs.
- File browser and file interaction flows work inside CanDoItAll.
- Large-desktop browser proof covers the shell and representative component-heavy pages.
- Container publish/build succeeds with sibling source contexts.
- The execution report contains exact commands, results, skips, SHAs, version `V`, and residual
  risks.
- The canonical merge path is `ui-refactoring -> development -> main`; v2 remains separate.

## Bundle map

- `README.cs.md` — Czech owner summary
- `prompt.md` — primary Codex execution prompt
- `inputs/` — original request and discovery baselines
- `analysis/` — branch, Components, FileTools, asset, and risk analysis
- `requirements/` — normalized requirements and non-goals
- `plan/` — phase plan and restart strategy
- `traceability/` — requirements-to-work/proof matrix
- `inventories/` — repository and known call-site inventories
- `commands/` — exact validation command catalog
- `shared-prompts/` — implementation and QA prompts
- `subbundles/` — nine executable subbundles
- `scripts/` — scope/version consistency helpers
- `reviews/` — self-review, readiness gate, and execution report template
- `proof/` — evidence placement contract
