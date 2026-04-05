## Repeat-offender summary
Several blocker themes are repeating because the previous closure criteria were too narrow:

- **Binding/carrier leak** was “refactored” but not retired. The fields were moved into `ProjectObjectRecord.LegacyCarrier.cs`, so the old gate missed them.
- **Marker dual truth** survived because the review did not yet force scalar marker retirement.
- **Plugin UI hardcoding** survived because the previous bundle checked only some enum-driven patterns, not manifest-driven rendering end-to-end.
- **Legacy enum identity** survived because the previous bundle warned about compatibility aliases but did not hard-fail on fake enum persistence in save flows.

Most importantly, the previous phase8 gate script produced a false green on this exact repo:

```text
=== Phase8 plugin-gate check ===
Repo: /mnt/data/review_phase9_current/CanDoItAll-canonical-model-refactor

No hard-gate failures detected.

Warnings:
- ADV WARNING: hotspot 'src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs' is still large (4969 lines > 4000).
- ADV WARNING: hotspot 'src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs' is still large (1161 lines > 1000).
```

That is why v9 adds repo-wide symbol-retirement gates and anti-evasion rules.
