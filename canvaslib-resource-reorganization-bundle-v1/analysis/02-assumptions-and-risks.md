# Assumptions And Risks

## Assumptions

- CanvasLib consumers should continue to load assets only through `CanvasLibHeadAssets.razor` and `CanvasLibBodyAssets.razor`, not by manually pinning the old monolithic asset URLs.
- Replacing one large public asset with an ordered sequence of smaller public files is acceptable if the include components remain the single source of truth.
- `ComponentKit` can stop publishing the duplicate CanvasLib asset set without breaking active code paths, because no source consumer currently references those URLs.

## Critical Path Risks

- The duplicate-retirement step is a critical foundation. If it is wrong, later proof may falsely show a healthy CanvasLib while the app is still loading assets from `ComponentKit`.
- The workbench runtime split is a critical foundation. If script order or shared namespace bootstrapping is wrong, structure-canvas routes will fail at runtime before any CSS or calendar proof is meaningful.
- The calendar split depends on the same manifest and include-generation machinery. If subbundle 02 weakens that machinery, the calendar proof becomes untrustworthy.

## Validation Risks

- Static-web-asset duplicates can hide behind build output and browser cache, so build and browser proof both need to confirm the shipped asset origin.
- The current asset builder is copy-based. If outputs are split but include ordering is wrong, errors will show up only in the browser or Playwright tests.
- Calendar and workbench use different surfaces, so a workbench-only smoke pass is not enough.
- Final size proof must audit the generated public outputs as well as the source fragments. Splitting only source files is insufficient because the user explicitly rejected super-long files in CanvasLib as a whole.

## Reopen Triggers

- Reopen subbundle 01 if any build, network trace, or source reference proves that an active consumer still depends on `_content/CanDoItAll.ComponentKit/...`.
- Reopen subbundle 02 if the structure canvas route fails, throws browser console errors, or loads runtime assets out of order after the split.
- Reopen subbundle 03 if the calendar route fails, the calendar asset list regresses, or any generated public calendar file still exceeds 2000 lines.
- Reopen subbundle 04 if the final line-count audit reports any file above 2000 lines anywhere under `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib`.
