# Current State Analysis

## CanDoItAll

The original branch and current development have diverged:

- original `ui-refactoring` is five commits ahead of its merge base,
- current `development` is approximately 87 commits ahead of that same base at discovery,
- the original branch changes only five files,
- current development has substantial product, CI, cross-platform, and test evolution.

This is not a broad two-sided UI merge. It is a current-development integration with five
small historical deltas plus downstream compatibility work for the new Components main.

### Original branch deltas and decisions

| Delta | Decision |
|---|---|
| Ignore `.idea/` | Keep, merged into current `.gitignore` |
| SDK `10.0.302 -> 10.0.204` | Reject; preserve current development SDK policy |
| Add root `npm run watch` | Keep, merged into current scripts |
| Load Material Symbols | Keep and extend to all raw icon consumers |
| Add `PODMAN.md` | Preserve useful content, relocate and modernize; remove stale root version |

## Components

The UI work is already merged into Components main, but the recorded post-merge CI is red.
Asset generation and compilation succeeded; three governance tests failed. Therefore:

- the merged public API/source/asset changes have not yet been explicitly approved,
- package build did not run,
- downstream integration must not declare success while this upstream remains red.

The CSS production contract also changed materially. `output.css` is ignored and generated
only by npm, while default downstream development consumes the project by source reference.
A clean CanDoItAll checkout with sibling source repositories cannot rely on a developer having
already run Components npm tooling.

### Prescribed source-consumer contract

Commit only the distributed BaseLib output:

```text
CanDoItAll.Components/
  src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css
```

Then enforce deterministic regeneration in Components CI. Continue ignoring generated sandbox
outputs. This preserves:

- zero hidden prerequisite for normal source-reference .NET builds,
- correct Docker source-context behavior,
- correct package static assets,
- one canonical stylesheet generated from the Components Tailwind source,
- no duplicated Components Tailwind compiler in the application repository.

## FileTools

FileTools does not currently consume Components. This is an intentional architecture boundary,
not a missing migration:

- component RCLs depend on FileTools layers and `Microsoft.AspNetCore.App`,
- package validation rejects `CanDoItAll.Components`,
- no `material-icons` usage was found,
- current CI is green.

Expected source-code changes in FileTools are therefore zero unless validation proves a real
independent defect. Package version cleanup is required.

## Versioning

Observed values are inconsistent across repositories. A coordinated new version avoids a
fallback graph where one repository requests package IDs that another repository would not
produce at the same version. Use one selected `V`, recommended candidate `0.3.0`, after checking
all configured feeds.

## Validation consequence

Compilation alone is insufficient. The integration crosses:

- static web assets,
- generated CSS,
- DOM/CSS selector contracts,
- source-reference substitution,
- package fallback,
- three operating-system CI jobs,
- Docker build contexts,
- FileTools host composition.

The bundle therefore requires source-mode, package-mode, browser, and container proof.
