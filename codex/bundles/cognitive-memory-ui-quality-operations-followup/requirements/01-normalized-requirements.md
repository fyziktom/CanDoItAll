# Normalized Requirements

| Id | Requirement | Proof |
|---|---|---|
| UI-01 | Preserve the raw request and imagegen proposal artifacts in the bundle. | Bundle inputs and prepared validator. |
| UI-02 | Add a large-screen-only design contract and remove medium/small-specific Cognitive Memory tuning. | Source diff and browser proof at large desktop only. |
| UI-03 | Every long Cognitive Memory list must have explicit paging UI. | Component test assertions and browser screenshot review. |
| UI-04 | The review UI service must apply page windows before materialization for list queries. | Unit tests and code review of query paths. |
| UI-05 | Snapshot data must include page metadata so badges and pagers show total/page state, not loaded-count-only state. | Unit tests on page metadata. |
| UI-06 | Add UI access to quality diagnostics. | Quality operations tab action and component test. |
| UI-07 | Add UI access to cluster planning and expose cluster result/list evidence. | Quality operations tab action, quality cluster list, and service test. |
| UI-08 | Add UI access to dream consolidation with clear dry-run versus persisted execution. | Quality operations tab action and component test. |
| UI-09 | Expose aggregate candidates and allow approved aggregate apply without hiding errors. | Quality operations tab aggregate list/action and unit/component test. |
| UI-10 | Expose synthesized recall/reference evidence at least as paged records or trace detail context. | Quality operations or recall trace UI and service test. |
| UI-11 | Improve every current tab on the module page with consistent desktop layout, counts, and paging where lists exist. | Component test and browser tab walk. |
| UI-12 | Do not add Radzen unless the page already uses it; keep BaseLib wrappers. | Source review. |
| UI-13 | Keep sensitive content masked and do not expose restricted references by default. | Existing quality tests plus no new unrestricted locator output in UI. |
| UI-14 | Complete with build/test/browser proof and bundle validators. | Execution report. |
