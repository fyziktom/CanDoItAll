# CanDoItAll — Storage Selection UI handoff

**Execute [prompt.md](prompt.md) with Codex GPT-6 Astra Max on the current checkout.**

First reproduce/repair the bounded catalog same-ID reselection issue, then extract the complete Storage catalog selection field/dialog family used by actual production editors. Keep the existing parent persistence and runtime access checks. This is not a Storage administration, Recovery, database-switching or permission-system rewrite.

Reviewed source: `fyziktom/CanDoItAll`, branch `components-decoupling`, commit `cbb135c7c8d76ff50a624c142f12faf8c9b55f91`. The SHA is provenance, not an execution pin. The preceding archived bundle must not be executed again.

## Read together

- [Review and SCAT-R1](STORAGE_REVIEW.md), [Workspace status](WORKSPACE_STATUS.md) and [current picker inventory](SELECTION_REVIEW_NOTES.md).
- [Architecture](ARCHITECTURE.md), [sensitive state](SENSITIVE_STATE.md), [shared v3](shared/README.md).
- [Validation matrix](VALIDATION_MATRIX.md), [application regressions](APPLICATION_REGRESSION_MATRIX.md), [development loop](DEV_LOOP.md).
- [Source register](SOURCES.md), [proof limits](PROOF_STATUS.md) and [package validation](PACKAGE_VALIDATION.md).

The package includes the same 22-file shared v3 foundation without recursive previous task archives. Current repository instructions and maintained ownership rules prevail over stale historical examples.

## Entry command

```text
Read and execute CanDoItAll_Workspace_Storage_Selection_UI_Decoupling/prompt.md.
Use the complete extracted package and current repository instructions.
First close SCAT-R1, then complete the Storage selection field/dialog family.
Preserve actual Agent save/runtime authority and all protected Workspace graphs.
Execute the application non-regression matrix. Do not start another slice.
```

## Scope of completion

The current Core/API/catalog administration architecture is retained. Shared selection is the next leaf. Placement Recovery, Data Sources and remaining settings renderer integrations are not fully extracted by this task; **Workspace must still be reported as partially complete**.

No product build/test/browser was executed in the review environment. Product claims require Codex's new proof; Python validation here is package-only.
